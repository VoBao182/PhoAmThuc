namespace VinhKhanhTourDemo;

internal static class SubscriptionState
{
    private const string SubscriptionExpiryKey = "sub_ngay_het_han";

    public static int CalculateRemainingDays(DateTime expiresAtUtc)
    {
        if (expiresAtUtc.Kind == DateTimeKind.Local)
            expiresAtUtc = expiresAtUtc.ToUniversalTime();

        return Math.Max(0, (int)Math.Floor((expiresAtUtc - DateTime.UtcNow).TotalDays));
    }

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
