using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<POI> POIs { get; set; } = [];
    public int TongPOI   { get; set; }
    public int TongMonAn { get; set; }
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

            TongPOI   = POIs.Count(p => p.TrangThai);
            TongMonAn = POIs.SelectMany(p => p.MonAns).Count(m => m.TinhTrang);
        }
        catch (Exception ex)
        {
            POIs = [];
            TongPOI = 0;
            TongMonAn = 0;
            ErrorMessage = $"Khong the tai du lieu tu database: {ex.GetBaseException().Message}";
        }
    }
}
