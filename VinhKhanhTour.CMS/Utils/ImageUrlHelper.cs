using VinhKhanhTour.Shared;

namespace VinhKhanhTour.CMS.Utils;

public static class ImageUrlHelper
{
    public static string? Resolve(string? imageUrl, string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var trimmedUrl = imageUrl.Trim();
        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.Host is "localhost" or "127.0.0.1" &&
                Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri) &&
                !string.Equals(apiUri.Host, absoluteUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return new UriBuilder(absoluteUri)
                {
                    Host = apiUri.Host,
                    Port = apiUri.Port
                }.Uri.ToString();
            }

            return absoluteUri.ToString();
        }

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            return trimmedUrl;

        var normalizedBase = apiBaseUrl.EndsWith('/')
            ? apiBaseUrl
            : apiBaseUrl + "/";

        return new Uri(new Uri(normalizedBase), trimmedUrl.TrimStart('/')).ToString();
    }

    public static string ResolvePoi(string? imageUrl, string? apiBaseUrl, string? poiName)
        => Resolve(imageUrl, apiBaseUrl)
        ?? Resolve(SharedImageCatalog.GetPoiFallbackImageUrl(poiName), apiBaseUrl)
        ?? SharedImageCatalog.RestaurantGenericPhoto;

    public static string ResolveDish(string? imageUrl, string? apiBaseUrl, string? dishName, string? category = null)
        => Resolve(imageUrl, apiBaseUrl)
        ?? Resolve(SharedImageCatalog.GetDishFallbackImageUrl(dishName, category), apiBaseUrl)
        ?? SharedImageCatalog.DishGenericPhoto;
}
