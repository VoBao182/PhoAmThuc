using System.Net.Http;
using System.Text.RegularExpressions;

namespace VinhKhanhTourDemo;

public static class AppConfig
{
    private const string CustomApiBaseUrlKey = "api_base_url_override";
    private const string LastGoodApiBaseUrlKey = "api_base_url_last_good";
    private static readonly SemaphoreSlim ResolveLock = new(1, 1);
    private static readonly TimeSpan ProbeCacheDuration = TimeSpan.FromMinutes(5);
    private static string? _resolvedApiBaseUrl;
    private static DateTime _lastProbeUtc = DateTime.MinValue;

    public static string ApiBaseUrl =>
        _resolvedApiBaseUrl
        ?? CustomApiBaseUrl
        ?? LastKnownGoodApiBaseUrl
        ?? DefaultApiBaseUrl;

    public static string DefaultApiBaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5118"
            : "http://localhost:5118";

    public static string? CustomApiBaseUrl =>
        NormalizeApiBaseUrl(Preferences.Get(CustomApiBaseUrlKey, ""));

    public static string? LastKnownGoodApiBaseUrl =>
        NormalizeApiBaseUrl(Preferences.Get(LastGoodApiBaseUrlKey, ""));

    public static async Task<string> EnsureApiBaseUrlAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_resolvedApiBaseUrl) &&
            DateTime.UtcNow - _lastProbeUtc < ProbeCacheDuration)
        {
            return _resolvedApiBaseUrl;
        }

        await ResolveLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_resolvedApiBaseUrl) &&
                DateTime.UtcNow - _lastProbeUtc < ProbeCacheDuration)
            {
                return _resolvedApiBaseUrl;
            }

            foreach (var candidate in GetCandidateApiBaseUrls())
            {
                if (await CanReachApiAsync(http, candidate, cancellationToken))
                {
                    RememberResolvedApiBaseUrl(candidate);
                    return candidate;
                }
            }

            _resolvedApiBaseUrl = CustomApiBaseUrl
                ?? LastKnownGoodApiBaseUrl
                ?? DefaultApiBaseUrl;
            _lastProbeUtc = DateTime.UtcNow;
            return _resolvedApiBaseUrl;
        }
        finally
        {
            ResolveLock.Release();
        }
    }

    public static void SetCustomApiBaseUrl(string apiBaseUrl)
    {
        var normalized = NormalizeApiBaseUrl(apiBaseUrl)
            ?? throw new ArgumentException("Invalid API URL.", nameof(apiBaseUrl));

        Preferences.Set(CustomApiBaseUrlKey, normalized);
        _resolvedApiBaseUrl = normalized;
        _lastProbeUtc = DateTime.MinValue;
    }

    public static void ClearCustomApiBaseUrl()
    {
        Preferences.Remove(CustomApiBaseUrlKey);
        _resolvedApiBaseUrl = null;
        _lastProbeUtc = DateTime.MinValue;
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

        return uri.GetLeftPart(UriPartial.Authority);
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

        AddCandidate(CustomApiBaseUrl);
        AddCandidate(LastKnownGoodApiBaseUrl);
        AddCandidate(DefaultApiBaseUrl);

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            AddCandidate("http://10.0.2.2:5118");
            AddCandidate("http://10.0.3.2:5118");
        }
        else
        {
            AddCandidate("http://127.0.0.1:5118");
            AddCandidate("http://localhost:5118");
        }

        return seen;
    }

    private static async Task<bool> CanReachApiAsync(HttpClient http, string apiBaseUrl, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            using var response = await http.GetAsync(
                $"{apiBaseUrl}/api/poi",
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Chuyển URL ảnh thành URL có thể tải được trên thiết bị hiện tại.
    /// - Đường dẫn tương đối (/uploads/...) → ghép với ApiBaseUrl
    /// - URL tuyệt đối có host localhost/127.0.0.1 → thay bằng host của ApiBaseUrl
    /// - URL ngoài (https://...) → giữ nguyên
    /// </summary>
    public static string ResolveImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        // Đường dẫn tương đối
        if (url.StartsWith('/'))
            return ApiBaseUrl.TrimEnd('/') + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        // URL absolute nhưng host là localhost → thay bằng host của API hiện tại
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
        Preferences.Set(LastGoodApiBaseUrlKey, normalized);
    }
}
