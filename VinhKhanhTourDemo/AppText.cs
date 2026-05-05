using System.Globalization;

namespace VinhKhanhTourDemo;

public static class AppText
{
    public static string LanguageCode => NormalizeLanguageCode(
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public static string T(string vi, string en, string zh)
        => LanguageCode switch
        {
            "en" => en,
            "zh" => zh,
            _ => vi
        };

    public static string PlanName(string planType)
        => planType switch
        {
            "ngay" => T("Gói Ngày", "Day Plan", "日套餐"),
            "tuan" => T("Gói Tuần", "Week Plan", "周套餐"),
            "thang" => T("Gói Tháng", "Month Plan", "月套餐"),
            "nam" => T("Gói Năm", "Year Plan", "年套餐"),
            "thu" => T("Dùng thử", "Free Trial", "免费试用"),
            _ => T("Gói Tháng", "Month Plan", "月套餐")
        };

    public static string PlanUsageDays(int days)
        => T($"{days} ngày sử dụng", $"{days} days of access", $"{days} 天使用期");

    public static string NormalizeLanguageCode(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "en" => "en",
            "zh" => "zh",
            _ => "vi"
        };
    }
}
