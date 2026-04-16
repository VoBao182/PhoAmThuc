namespace VinhKhanhTourDemo;

internal static class AppEndpointOptions
{
    public const int ApiPort = 5118;

    // Fallback for quick local edits. For release builds, prefer the HostedApiBaseUrl
    // value generated from the HOSTED_API_BASE_URL environment variable or /p:HostedApiBaseUrl.
    public const string HostedApiBaseUrlFallback = "";

    public static string HostedApiBaseUrl =>
        !string.IsNullOrWhiteSpace(BuildHostedApiBaseUrl.Value)
            ? BuildHostedApiBaseUrl.Value
            : HostedApiBaseUrlFallback;
}
