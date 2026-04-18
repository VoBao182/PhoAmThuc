using System.Globalization;
using System.Text;

namespace VinhKhanhTour.Shared;

public static class SharedImageCatalog
{
    public const string RestaurantGenericPhoto = "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?auto=format&fit=crop&w=1200&q=80";
    public const string RestaurantSeafoodPhoto = "https://images.unsplash.com/photo-1627900429100-3ed333915540?auto=format&fit=crop&w=1200&q=80";
    public const string RestaurantBeefGrillPhoto = "https://images.unsplash.com/photo-1566846186088-f9b804cb0640?auto=format&fit=crop&w=1200&q=80";
    public const string RestaurantHotpotPhoto = "https://images.unsplash.com/photo-1614104030967-5ca61a54247b?auto=format&fit=crop&w=1200&q=80";
    public const string RestaurantDessertPhoto = "https://images.unsplash.com/photo-1652463843090-9204717dbc6e?auto=format&fit=crop&w=1200&q=80";
    public const string RestaurantNoodlePhoto = "https://images.unsplash.com/photo-1657457320973-e8cf9b4d4a59?auto=format&fit=crop&w=1200&q=80";
    public const string RestaurantSidewalkPhoto = "https://images.unsplash.com/photo-1448043552756-e747b7a2b2b8?auto=format&fit=crop&w=1200&q=80";

    public const string DishGenericPhoto = "https://images.unsplash.com/photo-1467003909585-2f8a72700288?auto=format&fit=crop&w=1200&q=80";
    public const string DishSeafoodPhoto = "https://images.unsplash.com/photo-1727522793234-2e108fc0460b?auto=format&fit=crop&w=1200&q=80";
    public const string DishBeefPhoto = "https://images.unsplash.com/photo-1566846186088-f9b804cb0640?auto=format&fit=crop&w=1200&q=80";
    public const string DishGrillPhoto = "https://images.unsplash.com/photo-1566846186088-f9b804cb0640?auto=format&fit=crop&w=1200&q=80";
    public const string DishHotpotPhoto = "https://images.unsplash.com/photo-1567946008244-08f993981ba5?auto=format&fit=crop&w=1200&q=80";
    public const string DishNoodlePhoto = "https://images.unsplash.com/photo-1657457320973-e8cf9b4d4a59?auto=format&fit=crop&w=1200&q=80";
    public const string DishDessertPhoto = "https://images.unsplash.com/photo-1652463843090-9204717dbc6e?auto=format&fit=crop&w=1200&q=80";
    public const string DishDrinkPhoto = "https://images.unsplash.com/photo-1773632996574-45b0d56ff809?auto=format&fit=crop&w=1200&q=80";
    public const string DishSaladPhoto = "https://images.unsplash.com/photo-1571805341302-f857308690e3?auto=format&fit=crop&w=1200&q=80";

    public static string GetPoiFallbackImageUrl(string? poiName)
    {
        var text = Normalize(poiName);

        if (ContainsAny(text, "oc", "ngheu", "hai san", "seafood"))
            return RestaurantSeafoodPhoto;

        if (ContainsAny(text, "bo", "be", "nuong"))
            return RestaurantBeefGrillPhoto;

        if (ContainsAny(text, "lau", "hotpot"))
            return RestaurantHotpotPhoto;

        if (ContainsAny(text, "che", "dessert", "tra sua", "sweet"))
            return RestaurantDessertPhoto;

        if (ContainsAny(text, "bun", "pho", "mi", "hu tieu", "noodle"))
            return RestaurantNoodlePhoto;

        if (ContainsAny(text, "nhau", "via he", "an vat", "sidewalk"))
            return RestaurantSidewalkPhoto;

        return RestaurantGenericPhoto;
    }

    public static string GetDishFallbackImageUrl(string? dishName, string? category = null)
    {
        var text = Normalize($"{dishName} {category}");

        if (ContainsAny(text, "oc", "ngheu", "so diep", "so huyet", "muc", "ghe", "tom", "hai san", "seafood"))
            return DishSeafoodPhoto;

        if (ContainsAny(text, "bun", "pho", "hu tieu", "mi", "noodle"))
            return DishNoodlePhoto;

        if (ContainsAny(text, "che", "tau hu", "khuc bach", "sua tuoi", "dessert", "sweet"))
            return DishDessertPhoto;

        if (ContainsAny(text, "tra", "nuoc", "drink", "uong"))
            return DishDrinkPhoto;

        if (ContainsAny(text, "lau", "hotpot"))
            return DishHotpotPhoto;

        if (ContainsAny(text, "goi", "salad", "ngo sen"))
            return DishSaladPhoto;

        if (ContainsAny(text, "bo", "be", "la lot"))
            return DishBeefPhoto;

        if (ContainsAny(text, "nuong", "sa te", "kho muc", "long"))
            return DishGrillPhoto;

        return DishGenericPhoto;
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
