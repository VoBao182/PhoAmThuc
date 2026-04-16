namespace VinhKhanhTourDemo;

internal static class ApiConnectionPrompt
{
    public static async Task<string?> EnsureConnectedApiBaseUrlAsync(Page page, HttpClient http)
    {
        var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(http);
        if (await AppConfig.CanReachApiBaseUrlAsync(http, apiBaseUrl))
            return apiBaseUrl;

        if (AppConfig.HasConfiguredHostedApiBaseUrl)
        {
            await page.DisplayAlertAsync(
                "Khong ket noi duoc",
                AppConfig.BuildConnectionErrorMessage(new HttpRequestException("Unable to reach API.")),
                "OK");
            return null;
        }

        var shouldConfigure = await page.DisplayAlertAsync(
            "Khong ket noi duoc",
            AppConfig.BuildConnectionErrorMessage(new HttpRequestException("Unable to reach API.")),
            "Nhap API URL",
            "De sau");

        if (!shouldConfigure)
            return null;

        var initialValue = AppConfig.CustomApiBaseUrl
            ?? AppConfig.LastKnownGoodApiBaseUrl
            ?? AppConfig.ApiBaseUrl;

        var input = await page.DisplayPromptAsync(
            "API URL",
            AppConfig.BuildApiConnectionHelpText(),
            accept: "Luu",
            cancel: "Huy",
            placeholder: DeviceInfo.Platform == DevicePlatform.Android
                ? "http://192.168.1.5:5118"
                : "http://localhost:5118",
            initialValue: initialValue,
            keyboard: Keyboard.Url);

        var normalized = AppConfig.NormalizeApiBaseUrl(input);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                await page.DisplayAlertAsync(
                    "URL khong hop le",
                    "Hay nhap day du giao thuc va cong, vi du http://192.168.1.5:5118.",
                    "OK");
            }

            return null;
        }

        AppConfig.SetCustomApiBaseUrl(normalized);

        if (await AppConfig.CanReachApiBaseUrlAsync(http, normalized))
            return normalized;

        await page.DisplayAlertAsync(
            "Chua ket noi duoc",
            AppConfig.BuildConnectionErrorMessage(new HttpRequestException("Unable to reach API.")),
            "OK");

        return null;
    }
}
