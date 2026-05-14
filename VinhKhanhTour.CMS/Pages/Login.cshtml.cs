using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VinhKhanhTour.CMS.Pages;

public sealed class LoginModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
        => RedirectToLocal(ReturnUrl);

    public IActionResult OnPost()
        => RedirectToLocal(ReturnUrl);

    public IActionResult OnPostLogout()
        => RedirectToPage("/Index");

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToPage("/Index");
    }
}
