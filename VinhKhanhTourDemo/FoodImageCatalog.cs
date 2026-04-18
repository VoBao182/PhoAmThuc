using VinhKhanhTour.Shared;

namespace VinhKhanhTourDemo;

public static class FoodImageCatalog
{
    public static ImageSource GetPoiImageSource(string? imageUrl, string? poiName)
    {
        if (TryCreateImageSource(imageUrl, out var source))
            return source;

        if (TryCreateImageSource(SharedImageCatalog.GetPoiFallbackImageUrl(poiName), out source))
            return source;

        return ImageSource.FromFile("restaurant_generic.svg");
    }

    public static ImageSource GetDishImageSource(string? imageUrl, string? dishName, string? category = null)
    {
        if (TryCreateImageSource(imageUrl, out var source))
            return source;

        if (TryCreateImageSource(SharedImageCatalog.GetDishFallbackImageUrl(dishName, category), out source))
            return source;

        return ImageSource.FromFile("dish_generic.svg");
    }

    private static bool TryCreateImageSource(string? rawValue, out ImageSource source)
    {
        source = null!;

        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        var resolved = AppConfig.ResolveImageUrl(rawValue);
        if (string.IsNullOrWhiteSpace(resolved))
            return false;

        if (Uri.TryCreate(resolved, UriKind.Absolute, out var absoluteUri))
        {
            source = ImageSource.FromUri(absoluteUri);
            return true;
        }

        source = ImageSource.FromFile(resolved);
        return true;
    }
}
