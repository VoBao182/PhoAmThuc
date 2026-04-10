using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.Poi;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<POI> POIs { get; set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            POIs = await _db.POIs
                .AsNoTracking()
                .Include(p => p.MonAns)
                .OrderBy(p => p.MucUuTien)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            POIs = [];
            ErrorMessage = $"Khong the tai danh sach POI: {ex.GetBaseException().Message}";
        }
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var poi = await _db.POIs.FindAsync(id);
        if (poi != null)
        {
            poi.TrangThai = !poi.TrangThai;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Da {(poi.TrangThai ? "hien" : "an")} quan \"{poi.TenPOI}\"";
        }
        return RedirectToPage();
    }
}
