using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VinhKhanhTour.CMS.Pages.ThuyetMinh;

public class EditModel : PageModel
{
    public IActionResult OnGet(Guid? id)
    {
        if (id.HasValue)
            return Redirect($"/Poi/Edit/{id.Value}");

        return RedirectToPage("/ThuyetMinh/Index");
    }
}
