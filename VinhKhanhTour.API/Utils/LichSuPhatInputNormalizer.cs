namespace VinhKhanhTour.API.Utils;

internal static class LichSuPhatInputNormalizer
{
    private const string RecoveryPrefix = "VKT-DEVICE:";

    public static string NormalizeNguon(string? nguon, string fallback = "GPS")
    {
        if (string.IsNullOrWhiteSpace(nguon))
            return fallback;

        return nguon.Trim().ToUpperInvariant() switch
        {
            "GPS"          => "GPS",
            "QR"           => "QR",
            "APP-GEOFENCE" => "GPS",
            "APP_GEOFENCE" => "GPS",
            "GEOFENCE"     => "GPS",
            "QRCODE"       => "QR",
            "QR-CODE"      => "QR",
            "VIEW"         => "VIEW",
            _              => fallback
        };
    }

    public static string NormalizeNgonNgu(string? ngonNgu, string fallback = "vi")
    {
        if (string.IsNullOrWhiteSpace(ngonNgu))
            return fallback;

        return ngonNgu.Trim().ToLowerInvariant() switch
        {
            "vi" => "vi",
            "en" => "en",
            "zh" => "zh",
            _ => fallback
        };
    }

    public static string NormalizeMaThietBi(string maThietBi)
    {
        if (string.IsNullOrWhiteSpace(maThietBi))
            return "";

        var value = maThietBi.Trim();
        if (value.StartsWith(RecoveryPrefix, StringComparison.OrdinalIgnoreCase))
            value = value[RecoveryPrefix.Length..];

        return value.Trim().ToLowerInvariant();
    }
}
