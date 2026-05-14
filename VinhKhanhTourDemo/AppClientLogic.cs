using System.Net;
using System.Text.RegularExpressions;

namespace VinhKhanhTourDemo;

internal static class AppClientLogic
{
    public static int CalculateRemainingDays(DateTime expiresAtUtc, DateTime nowUtc)
    {
        if (expiresAtUtc.Kind == DateTimeKind.Local)
            expiresAtUtc = expiresAtUtc.ToUniversalTime();

        if (nowUtc.Kind == DateTimeKind.Local)
            nowUtc = nowUtc.ToUniversalTime();

        return Math.Max(0, (int)Math.Floor((expiresAtUtc - nowUtc).TotalDays));
    }

    public static string? NormalizeApiBaseUrl(string? rawUrl, bool preferHttpsForPublicHosts = true)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        var normalized = rawUrl.Trim();
        normalized = Regex.Replace(normalized, "/+$", "");

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        if (preferHttpsForPublicHosts && uri.Scheme == Uri.UriSchemeHttp && ShouldPreferHttps(uri))
        {
            uri = new UriBuilder(uri)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = -1
            }.Uri;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static string ResolveImageUrl(string? url, string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        if (url.StartsWith('/'))
            return apiBaseUrl.TrimEnd('/') + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        if (uri.Host is "localhost" or "127.0.0.1" &&
            Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri) &&
            apiUri.Host != uri.Host)
        {
            return new UriBuilder(uri)
            {
                Scheme = apiUri.Scheme,
                Host = apiUri.Host,
                Port = apiUri.Port
            }.Uri.ToString();
        }

        return url;
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
