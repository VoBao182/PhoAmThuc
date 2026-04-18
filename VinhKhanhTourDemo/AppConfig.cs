using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

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
            ? $"http://127.0.0.1:{AppEndpointOptions.ApiPort}"
            : $"http://localhost:{AppEndpointOptions.ApiPort}");

    public static string? ConfiguredHostedApiBaseUrl =>
        NormalizeApiBaseUrl(AppEndpointOptions.HostedApiBaseUrl);

    public static bool HasConfiguredHostedApiBaseUrl =>
        !string.IsNullOrWhiteSpace(ConfiguredHostedApiBaseUrl);

    public static bool AllowManualApiOverride =>
        !HasConfiguredHostedApiBaseUrl;

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

            _resolvedApiBaseUrl = CustomApiBaseUrl
                ?? LastKnownGoodApiBaseUrl
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
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var normalized = rawUrl.Trim();
        normalized = Regex.Replace(normalized, "/+$", "");

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        if (uri.Scheme == Uri.UriSchemeHttp && ShouldPreferHttps(uri))
        {
            uri = new UriBuilder(uri)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = -1
            }.Uri;
        }

        return uri.GetLeftPart(UriPartial.Authority);
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
            return $"Ung dung dang duoc cau hinh dung public API: {ConfiguredHostedApiBaseUrl}. Hay kiem tra backend/public domain dang online.";

        return DeviceInfo.Platform == DevicePlatform.Android
            ? $"Ban dev local: uu tien dung public API. Neu dang test qua USB, hay bat adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort} de app tu dung localhost ma khong can nhap IP."
            : $"Neu backend khong chay cung may, hay cau hinh mot public API URL, vi du https://api.vinhkhanhtour.vn.";
    }

    public static string BuildConnectionErrorMessage(Exception exception)
    {
        var apiBaseUrl = ApiBaseUrl;

        if (HasConfiguredHostedApiBaseUrl)
        {
            return $"Khong ket noi duoc toi {apiBaseUrl}. Day phai la public API cho khach hang, hay kiem tra server/domain dang hoat dong. Chi tiet: {exception.Message}";
        }

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return $"Khong ket noi duoc toi {apiBaseUrl}. Neu ban dang demo APK chua gan backend public, hay vao Cai dat de nhap API URL thu cong; con khi test USB local thi dung adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort}.";
        }

        return $"Khong ket noi duoc toi {apiBaseUrl}. Hay kiem tra backend dang chay va URL API dung. Chi tiet: {exception.Message}";
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
            AddCandidate($"http://127.0.0.1:{AppEndpointOptions.ApiPort}");
            AddCandidate($"http://localhost:{AppEndpointOptions.ApiPort}");
            AddCandidate($"http://10.0.2.2:{AppEndpointOptions.ApiPort}");
            AddCandidate($"http://10.0.3.2:{AppEndpointOptions.ApiPort}");
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
        if (string.IsNullOrWhiteSpace(url))
            return "";

        if (url.StartsWith('/'))
            return ApiBaseUrl.TrimEnd('/') + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        if (uri.Host is "localhost" or "127.0.0.1" &&
            Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var apiUri) &&
            apiUri.Host != uri.Host)
        {
            return new UriBuilder(uri) { Host = apiUri.Host, Port = apiUri.Port }.Uri.ToString();
        }

        return url;
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

    private static bool ShouldPreferHttps(Uri uri)
    {
        if (uri.IsLoopback)
            return false;

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return !IPAddress.TryParse(host, out _);
    }
}
