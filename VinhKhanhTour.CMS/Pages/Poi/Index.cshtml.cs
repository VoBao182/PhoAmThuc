using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.Poi;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    public IndexModel(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public List<POI> POIs { get; set; } = [];
    public string? ErrorMessage { get; private set; }
    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5118";

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
            ErrorMessage = $"Không thể tải danh sách POI: {ex.GetBaseException().Message}";
        }
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var poi = await _db.POIs.FindAsync(id);
        if (poi != null)
        {
            poi.TrangThai = !poi.TrangThai;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Đã {(poi.TrangThai ? "hiện" : "ẩn")} quán \"{poi.TenPOI}\"";
        }
        return RedirectToPage();
    }
}
