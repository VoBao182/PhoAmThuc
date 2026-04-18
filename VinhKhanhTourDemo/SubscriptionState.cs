namespace VinhKhanhTourDemo;

internal static class SubscriptionState
{
    private const string SubscriptionExpiryKey = "sub_ngay_het_han";

    public static bool HasStoredSubscriptionRecord()
        => Preferences.ContainsKey(SubscriptionExpiryKey);

    public static bool IsSubscriptionActive()
    {
        var expiryRaw = Preferences.Get(SubscriptionExpiryKey, "");
        if (string.IsNullOrWhiteSpace(expiryRaw))
            return false;

        return DateTime.TryParse(
                   expiryRaw,
                   null,
                   System.Globalization.DateTimeStyles.RoundtripKind,
                   out var expiryUtc)
               && expiryUtc > DateTime.UtcNow;
    }
}
