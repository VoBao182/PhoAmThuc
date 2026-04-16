namespace VinhKhanhTour.API.Utils;

internal static class LichSuPhatInputNormalizer
{
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
        => maThietBi.Trim();
}
