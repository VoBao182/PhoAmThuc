using System.Net.Http;

namespace VinhKhanhTourDemo;

public static class AppConfig
{
#if DEBUG
    public const bool IsDebugBuild = true;
#else
    public const bool IsDebugBuild = false;
#endif

    private const string CustomApiBaseUrlKey = "api_base_url_override";
    private const string LastGoodApiBaseUrlKey = "api_base_url_last_good";
    private static readonly SemaphoreSlim ResolveLock = new(1, 1);
    private static readonly TimeSpan SuccessfulProbeCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FailedProbeCacheDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HostedApiProbeTimeout = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan LocalApiProbeTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan HostedApiRequestTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan LocalApiRequestTimeout = TimeSpan.FromSeconds(15);
    private static string? _resolvedApiBaseUrl;
    private static DateTime _lastProbeUtc = DateTime.MinValue;
    private static bool _lastProbeSucceeded;

    public static string ApiBaseUrl =>
        ConfiguredHostedApiBaseUrl
        ?? _resolvedApiBaseUrl
        ?? CustomApiBaseUrl
        ?? LastKnownGoodApiBaseUrl
        ?? DefaultApiBaseUrl;

    public static string DefaultApiBaseUrl =>
        ConfiguredHostedApiBaseUrl
        ?? (DeviceInfo.Platform == DevicePlatform.Android
            ? $"http://10.0.2.2:{AppEndpointOptions.ApiPort}"
            : $"http://localhost:{AppEndpointOptions.ApiPort}");

    public static string? ConfiguredHostedApiBaseUrl =>
        NormalizeApiBaseUrl(AppEndpointOptions.HostedApiBaseUrl);

    public static bool HasConfiguredHostedApiBaseUrl =>
        !string.IsNullOrWhiteSpace(ConfiguredHostedApiBaseUrl);

    public static bool AllowManualApiOverride =>
        !HasConfiguredHostedApiBaseUrl;

    public static TimeSpan PreferredApiRequestTimeout =>
        HasConfiguredHostedApiBaseUrl
            ? HostedApiRequestTimeout
            : LocalApiRequestTimeout;

    public static string? CustomApiBaseUrl =>
        AllowManualApiOverride
            ? NormalizeApiBaseUrl(Preferences.Get(CustomApiBaseUrlKey, ""))
            : null;

    public static string? LastKnownGoodApiBaseUrl =>
        AllowManualApiOverride
            ? NormalizeApiBaseUrl(Preferences.Get(LastGoodApiBaseUrlKey, ""))
            : null;

    public static async Task<string> EnsureApiBaseUrlAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        var cacheDuration = _lastProbeSucceeded ? SuccessfulProbeCacheDuration : FailedProbeCacheDuration;
        if (!string.IsNullOrWhiteSpace(_resolvedApiBaseUrl) &&
            DateTime.UtcNow - _lastProbeUtc < cacheDuration)
        {
            return _resolvedApiBaseUrl;
        }

        await ResolveLock.WaitAsync(cancellationToken);
        try
        {
            cacheDuration = _lastProbeSucceeded ? SuccessfulProbeCacheDuration : FailedProbeCacheDuration;
            if (!string.IsNullOrWhiteSpace(_resolvedApiBaseUrl) &&
                DateTime.UtcNow - _lastProbeUtc < cacheDuration)
            {
                return _resolvedApiBaseUrl;
            }

            foreach (var candidate in GetCandidateApiBaseUrls())
            {
                if (await ProbeApiAsync(http, candidate, cancellationToken))
                {
                    RememberResolvedApiBaseUrl(candidate);
                    return candidate;
                }
            }

            _resolvedApiBaseUrl = LastKnownGoodApiBaseUrl
                ?? DefaultApiBaseUrl;
            _lastProbeUtc = DateTime.UtcNow;
            _lastProbeSucceeded = false;
            return _resolvedApiBaseUrl;
        }
        finally
        {
            ResolveLock.Release();
        }
    }

    public static void SetCustomApiBaseUrl(string apiBaseUrl)
    {
        if (!AllowManualApiOverride)
            return;

        var normalized = NormalizeApiBaseUrl(apiBaseUrl)
            ?? throw new ArgumentException("Invalid API URL.", nameof(apiBaseUrl));

        Preferences.Set(CustomApiBaseUrlKey, normalized);
        _resolvedApiBaseUrl = normalized;
        _lastProbeUtc = DateTime.MinValue;
        _lastProbeSucceeded = false;
    }

    public static void ClearCustomApiBaseUrl()
    {
        Preferences.Remove(LastGoodApiBaseUrlKey);
        Preferences.Remove(CustomApiBaseUrlKey);
        _resolvedApiBaseUrl = null;
        _lastProbeUtc = DateTime.MinValue;
        _lastProbeSucceeded = false;
    }

    public static string? NormalizeApiBaseUrl(string? rawUrl)
    {
        return AppClientLogic.NormalizeApiBaseUrl(rawUrl);
    }

    public static async Task<bool> CanReachApiBaseUrlAsync(
        HttpClient http,
        string? apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeApiBaseUrl(apiBaseUrl);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return await ProbeApiAsync(http, normalized, cancellationToken);
    }

    public static string BuildApiConnectionHelpText()
    {
        if (HasConfiguredHostedApiBaseUrl)
            return AppText.T(
                $"Ứng dụng đang được cấu hình dùng public API: {ConfiguredHostedApiBaseUrl}. Hãy kiểm tra backend/public domain đang online.",
                $"The app is configured to use the public API: {ConfiguredHostedApiBaseUrl}. Check that the backend/public domain is online.",
                $"应用已配置使用公共 API：{ConfiguredHostedApiBaseUrl}。请检查后端/公共域名是否在线。");

        return DeviceInfo.Platform == DevicePlatform.Android
            ? AppText.T(
                $"Bản dev local: ưu tiên dùng public API. Nếu đang test qua USB, hãy bật adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort} để app tự dùng localhost mà không cần nhập IP.",
                $"Local dev build: public API is preferred. For USB testing, enable adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort} so the app can use localhost without entering an IP.",
                $"本地开发版优先使用公共 API。USB 测试时请启用 adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort}，应用即可使用 localhost。")
            : AppText.T(
                "Nếu backend không chạy cùng máy, hãy cấu hình một public API URL, ví dụ https://api.vinhkhanhtour.vn.",
                "If the backend is not running on this machine, configure a public API URL, for example https://api.vinhkhanhtour.vn.",
                "如果后端不在同一台机器运行，请配置公共 API URL，例如 https://api.vinhkhanhtour.vn。");
    }

    public static string BuildConnectionErrorMessage(Exception exception)
    {
        var apiBaseUrl = ApiBaseUrl;

        if (HasConfiguredHostedApiBaseUrl)
        {
            return AppText.T(
                $"Không kết nối được tới {apiBaseUrl}. Đây phải là public API cho khách hàng, hãy kiểm tra server/domain đang hoạt động. Chi tiết: {exception.Message}",
                $"Cannot connect to {apiBaseUrl}. This must be the public customer API; check that the server/domain is online. Details: {exception.Message}",
                $"无法连接到 {apiBaseUrl}。这应是面向客户的公共 API，请检查服务器/域名是否在线。详情：{exception.Message}");
        }

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return AppText.T(
                $"Không kết nối được tới {apiBaseUrl}. Nếu bạn đang demo APK chưa gắn backend public, hãy vào Cài đặt để nhập API URL thủ công; còn khi test USB local thì dùng adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort}.",
                $"Cannot connect to {apiBaseUrl}. If this APK demo is not connected to a public backend, open Settings and enter the API URL manually; for local USB testing, use adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort}.",
                $"无法连接到 {apiBaseUrl}。如果此 APK 演示未连接公共后端，请在设置中手动输入 API URL；本地 USB 测试请使用 adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort}。");
        }

        return AppText.T(
            $"Không kết nối được tới {apiBaseUrl}. Hãy kiểm tra backend đang chạy và URL API đúng. Chi tiết: {exception.Message}",
            $"Cannot connect to {apiBaseUrl}. Check that the backend is running and the API URL is correct. Details: {exception.Message}",
            $"无法连接到 {apiBaseUrl}。请检查后端是否运行以及 API URL 是否正确。详情：{exception.Message}");
    }

    private static IEnumerable<string> GetCandidateApiBaseUrls()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? value)
        {
            var normalized = NormalizeApiBaseUrl(value);
            if (!string.IsNullOrWhiteSpace(normalized))
                seen.Add(normalized);
        }

        AddCandidate(ConfiguredHostedApiBaseUrl);

        if (HasConfiguredHostedApiBaseUrl)
            return seen;

        AddCandidate(CustomApiBaseUrl);
        AddCandidate(LastKnownGoodApiBaseUrl);
        AddCandidate(DefaultApiBaseUrl);

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            AddCandidate($"http://10.0.2.2:{AppEndpointOptions.ApiPort}");
            AddCandidate($"http://10.0.3.2:{AppEndpointOptions.ApiPort}");
            AddCandidate($"http://127.0.0.1:{AppEndpointOptions.ApiPort}");
            AddCandidate($"http://localhost:{AppEndpointOptions.ApiPort}");
        }
        else
        {
            AddCandidate($"http://127.0.0.1:{AppEndpointOptions.ApiPort}");
            AddCandidate($"http://localhost:{AppEndpointOptions.ApiPort}");
        }

        return seen;
    }

    private static async Task<bool> ProbeApiAsync(HttpClient http, string apiBaseUrl, CancellationToken cancellationToken)
    {
        var probeTimeout = GetProbeTimeout(apiBaseUrl);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(probeTimeout);
        using var probeHttp = CreateProbeHttpClient(probeTimeout);

        try
        {
            using var response = await probeHttp.GetAsync(
                $"{apiBaseUrl}/health",
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            try
            {
                using var fallbackResponse = await probeHttp.GetAsync(
                    $"{apiBaseUrl}/api/poi",
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);

                return fallbackResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    private static TimeSpan GetProbeTimeout(string apiBaseUrl)
    {
        var normalized = NormalizeApiBaseUrl(apiBaseUrl);
        if (!string.IsNullOrWhiteSpace(normalized) &&
            !string.IsNullOrWhiteSpace(ConfiguredHostedApiBaseUrl) &&
            string.Equals(normalized, ConfiguredHostedApiBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return HostedApiProbeTimeout;
        }

        return LocalApiProbeTimeout;
    }

    private static HttpClient CreateProbeHttpClient(TimeSpan timeout)
    {
        return new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            Timeout = timeout + TimeSpan.FromSeconds(5)
        };
    }

    public static string ResolveImageUrl(string? url)
    {
        return AppClientLogic.ResolveImageUrl(url, ApiBaseUrl);
    }

    private static void RememberResolvedApiBaseUrl(string apiBaseUrl)
    {
        var normalized = NormalizeApiBaseUrl(apiBaseUrl);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        _resolvedApiBaseUrl = normalized;
        _lastProbeUtc = DateTime.UtcNow;
        _lastProbeSucceeded = true;
        Preferences.Set(LastGoodApiBaseUrlKey, normalized);
    }

}
