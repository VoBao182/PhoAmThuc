using Android.Provider;

namespace VinhKhanhTourDemo;

internal static partial class DeviceIdentity
{
    private static partial string? GetAndroidDeviceIdentifier()
    {
        var context = Android.App.Application.Context;
        var androidId = Settings.Secure.GetString(
            context.ContentResolver,
            Settings.Secure.AndroidId);

        return string.IsNullOrWhiteSpace(androidId)
            ? null
            : $"android:{androidId}";
    }
}
