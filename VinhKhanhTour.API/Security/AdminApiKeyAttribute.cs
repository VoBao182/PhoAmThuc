using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace VinhKhanhTour.API.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Token";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var configuredToken = configuration["AdminApi:Token"]
            ?? Environment.GetEnvironmentVariable("ADMIN_API_TOKEN");

        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            context.Result = new ObjectResult(new { message = "Admin API token is not configured." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        var request = context.HttpContext.Request;
        var providedToken = request.Headers.TryGetValue(HeaderName, out var headerValues)
            ? headerValues.ToString()
            : GetBearerToken(request.Headers.Authorization.ToString());

        if (!TokenMatches(providedToken, configuredToken))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Admin token is required." });
        }
    }

    private static string? GetBearerToken(string authorizationHeader)
    {
        const string bearerPrefix = "Bearer ";
        return authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[bearerPrefix.Length..]
            : null;
    }

    private static bool TokenMatches(string? providedToken, string configuredToken)
    {
        if (string.IsNullOrWhiteSpace(providedToken))
            return false;

        var providedBytes = Encoding.UTF8.GetBytes(providedToken);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredToken);
        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
