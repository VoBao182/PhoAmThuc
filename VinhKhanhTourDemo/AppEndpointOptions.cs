namespace VinhKhanhTourDemo;

internal static class AppEndpointOptions
{
    public const int ApiPort = 5118;

    // Default API used by debug APKs and ad-hoc local runs. Override with the
    // HOSTED_API_BASE_URL environment variable or /p:HostedApiBaseUrl when needed.
    public const string HostedApiBaseUrlFallback = "https://phoamthuc.onrender.com";

    public static string HostedApiBaseUrl =>
        !string.IsNullOrWhiteSpace(BuildHostedApiBaseUrl.Value)
            ? BuildHostedApiBaseUrl.Value
            : HostedApiBaseUrlFallback;
}
