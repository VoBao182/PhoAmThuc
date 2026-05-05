namespace VinhKhanhTourDemo;

internal static class ApiConnectionPrompt
{
    public static async Task<string?> EnsureConnectedApiBaseUrlAsync(Page page, HttpClient http)
    {
        var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(http);
        if (AppConfig.HasConfiguredHostedApiBaseUrl)
            return apiBaseUrl;

        if (await AppConfig.CanReachApiBaseUrlAsync(http, apiBaseUrl))
            return apiBaseUrl;

        var shouldConfigure = await page.DisplayAlertAsync(
            AppText.T("Không kết nối được", "Connection failed", "连接失败"),
            AppConfig.BuildConnectionErrorMessage(new HttpRequestException("Unable to reach API.")),
            AppText.T("Nhập API URL", "Enter API URL", "输入 API URL"),
            AppText.T("Để sau", "Later", "稍后"));

        return shouldConfigure
            ? await PromptForApiBaseUrlAsync(page, http)
            : null;
    }

    public static async Task<string?> PromptForApiBaseUrlAsync(Page page, HttpClient http)
    {
        var initialValue = AppConfig.CustomApiBaseUrl
            ?? AppConfig.LastKnownGoodApiBaseUrl
            ?? AppConfig.ApiBaseUrl;

        var input = await page.DisplayPromptAsync(
            "API URL",
            AppConfig.BuildApiConnectionHelpText(),
            accept: AppText.T("Lưu", "Save", "保存"),
            cancel: AppText.T("Hủy", "Cancel", "取消"),
            placeholder: DeviceInfo.Platform == DevicePlatform.Android
                ? "http://127.0.0.1:5118"
                : "http://localhost:5118",
            initialValue: initialValue,
            keyboard: Keyboard.Url);

        var normalized = AppConfig.NormalizeApiBaseUrl(input);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                await page.DisplayAlertAsync(
                    AppText.T("URL không hợp lệ", "Invalid URL", "URL 无效"),
                    AppText.T(
                        "Hãy nhập đầy đủ giao thức và cổng, ví dụ http://127.0.0.1:5118.",
                        "Enter the full protocol and port, for example http://127.0.0.1:5118.",
                        "请输入完整协议和端口，例如 http://127.0.0.1:5118。"),
                    "OK");
            }

            return null;
        }

        AppConfig.SetCustomApiBaseUrl(normalized);

        if (await AppConfig.CanReachApiBaseUrlAsync(http, normalized))
            return normalized;

        await page.DisplayAlertAsync(
            AppText.T("Chưa kết nối được", "Still cannot connect", "仍无法连接"),
            AppConfig.BuildConnectionErrorMessage(new HttpRequestException("Unable to reach API.")),
            "OK");

        return null;
    }
}
