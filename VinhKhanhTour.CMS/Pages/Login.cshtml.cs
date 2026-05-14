using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VinhKhanhTour.CMS.Pages;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly IConfiguration _configuration;

    public LoginModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    [Required]
    public string Username { get; set; } = "";

    [BindProperty]
    [Required]
    public string Password { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(ReturnUrl);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var configuredUsername = GetConfiguredValue("CmsAdmin:Username", "CMS_ADMIN_USERNAME");
        var configuredPassword = GetConfiguredValue("CmsAdmin:Password", "CMS_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(configuredUsername) || string.IsNullOrWhiteSpace(configuredPassword))
        {
            ModelState.AddModelError(string.Empty, "CMS admin credentials are not configured.");
            return Page();
        }

        if (!SecureEquals(Username, configuredUsername) || !SecureEquals(Password, configuredPassword))
        {
            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không đúng.");
            return Page();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, configuredUsername),
            new Claim(ClaimTypes.Role, "CmsAdmin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToLocal(ReturnUrl);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }

    private string? GetConfiguredValue(string configKey, string envKey)
    {
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue;

        return _configuration[configKey];
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToPage("/Index");
    }

    private static bool SecureEquals(string provided, string configured)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        if (providedBytes.Length != configuredBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
