namespace VinhKhanhTour.CMS.Utils;

public static class ImageUrlHelper
{
    public static string? Resolve(string? imageUrl, string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var trimmedUrl = imageUrl.Trim();
        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            return trimmedUrl;

        var normalizedBase = apiBaseUrl.EndsWith('/')
            ? apiBaseUrl
            : apiBaseUrl + "/";

        return new Uri(new Uri(normalizedBase), trimmedUrl.TrimStart('/')).ToString();
    }
}
