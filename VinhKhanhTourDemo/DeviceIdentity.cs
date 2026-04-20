using System.Security.Cryptography;
using System.Text;

namespace VinhKhanhTourDemo;

internal static partial class DeviceIdentity
{
    private const string DeviceIdPreferenceKey = "device_id";
    private const string DeviceIdOverridePreferenceKey = "device_id_override";
    private const string RecoveryPrefix = "VKT-DEVICE:";

    public static string GetDeviceId()
    {
        var overrideId = NormalizeRecoveryCode(Preferences.Get(DeviceIdOverridePreferenceKey, ""));
        if (!string.IsNullOrWhiteSpace(overrideId))
        {
            Preferences.Set(DeviceIdPreferenceKey, overrideId);
            return overrideId;
        }

        var savedId = NormalizeRecoveryCode(Preferences.Get(DeviceIdPreferenceKey, ""));
        if (!string.IsNullOrWhiteSpace(savedId))
            return savedId;

        var stablePlatformId = BuildStableDeviceId(GetPlatformDeviceIdentifier());
        if (!string.IsNullOrWhiteSpace(stablePlatformId))
        {
            Preferences.Set(DeviceIdPreferenceKey, stablePlatformId);
            return stablePlatformId;
        }

        // Last-resort fallback for platforms that do not expose a stable ID.
        var generatedId = Guid.NewGuid().ToString("N");
        Preferences.Set(DeviceIdPreferenceKey, generatedId);
        return generatedId;
    }

    public static string GetShortDeviceId()
    {
        var deviceId = GetDeviceId();
        return deviceId[..Math.Min(8, deviceId.Length)].ToUpperInvariant();
    }

    public static string BuildRecoveryPayload()
        => $"{RecoveryPrefix}{GetDeviceId()}";

    public static string BuildQrCodeUrl()
    {
        var payload = Uri.EscapeDataString(BuildRecoveryPayload());
        return $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&margin=10&data={payload}";
    }

    public static bool TrySetDeviceIdOverride(string? rawCode, out string deviceId)
    {
        deviceId = NormalizeRecoveryCode(rawCode);
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;

        Preferences.Set(DeviceIdOverridePreferenceKey, deviceId);
        Preferences.Set(DeviceIdPreferenceKey, deviceId);
        return true;
    }

    private static string NormalizeRecoveryCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return "";

        var value = rawCode.Trim();
        if (value.StartsWith(RecoveryPrefix, StringComparison.OrdinalIgnoreCase))
            value = value[RecoveryPrefix.Length..];

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.Length is >= 8 and <= 128
            ? builder.ToString()
            : "";
    }

    private static string BuildStableDeviceId(string? platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId))
            return "";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(platformId.Trim()));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }

    private static string? GetPlatformDeviceIdentifier()
    {
#if ANDROID
        return GetAndroidDeviceIdentifier();
#else
        return $"{DeviceInfo.Platform}:{DeviceInfo.Manufacturer}:{DeviceInfo.Model}:{DeviceInfo.Name}";
#endif
    }

#if ANDROID
    private static partial string? GetAndroidDeviceIdentifier();
#endif
}
