using System.Globalization;
using System.Text;

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
        => Resolve(imageUrl, apiBaseUrl) ?? GetPoiFallbackUrl(poiName);

    public static string ResolveDish(string? imageUrl, string? apiBaseUrl, string? dishName, string? category = null)
        => Resolve(imageUrl, apiBaseUrl) ?? GetDishFallbackUrl(dishName, category);

    private static string GetPoiFallbackUrl(string? poiName)
    {
        var text = Normalize(poiName);

        if (ContainsAny(text, "oc", "ngheu", "hai san", "seafood"))
            return "/images/fallback/restaurant_seafood.svg";

        if (ContainsAny(text, "bo", "be", "nuong", "nhau", "via he", "an vat"))
            return "/images/fallback/restaurant_grill.svg";

        if (ContainsAny(text, "lau", "hotpot"))
            return "/images/fallback/restaurant_hotpot.svg";

        if (ContainsAny(text, "che", "dessert", "tra sua", "sweet"))
            return "/images/fallback/restaurant_dessert.svg";

        if (ContainsAny(text, "bun", "pho", "mi", "hu tieu", "noodle"))
            return "/images/fallback/restaurant_noodle.svg";

        return "/images/fallback/restaurant_generic.svg";
    }

    private static string GetDishFallbackUrl(string? dishName, string? category)
    {
        var text = Normalize($"{dishName} {category}");

        if (ContainsAny(text, "oc", "ngheu", "so diep", "so huyet", "muc", "ghe", "tom", "hai san", "seafood"))
            return "/images/fallback/dish_seafood.svg";

        if (ContainsAny(text, "bun", "pho", "hu tieu", "mi", "noodle"))
            return "/images/fallback/dish_noodle.svg";

        if (ContainsAny(text, "che", "tau hu", "khuc bach", "sua tuoi", "dessert", "sweet"))
            return "/images/fallback/dish_dessert.svg";

        if (ContainsAny(text, "tra", "nuoc", "drink", "uong"))
            return "/images/fallback/dish_drink.svg";

        if (ContainsAny(text, "lau", "hotpot"))
            return "/images/fallback/dish_hotpot.svg";

        if (ContainsAny(text, "goi", "salad", "ngo sen"))
            return "/images/fallback/dish_salad.svg";

        if (ContainsAny(text, "bo", "be", "nuong", "sa te", "kho muc", "long"))
            return "/images/fallback/dish_grill.svg";

        return "/images/fallback/dish_generic.svg";
    }

    private static bool ContainsAny(string source, params string[] keywords)
        => keywords.Any(source.Contains);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value
            .Trim()
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
