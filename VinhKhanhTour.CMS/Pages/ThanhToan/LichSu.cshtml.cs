using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.ThanhToan;

public class HoaDonItem
{
    public string   LoaiPhi       { get; set; } = "";
    public string?  KyThanhToan   { get; set; }
    public decimal  SoTien        { get; set; }
    public DateTime NgayThanhToan { get; set; }
    public string?  GhiChu        { get; set; }
}

public class LichSuModel : PageModel
{
    private readonly AppDbContext _db;
    public LichSuModel(AppDbContext db) => _db = db;

    public Guid   PoiId   { get; set; }
    public string TenPOI  { get; set; } = "";
    public List<HoaDonItem> HoaDons { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid poiId)
    {
        PoiId = poiId;
        var poi = await _db.POIs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == poiId);
        if (poi == null) return NotFound();

        TenPOI = poi.TenPOI;

        HoaDons = await _db.HoaDons.AsNoTracking()
            .Where(h => h.POIId == poiId)
            .OrderByDescending(h => h.NgayThanhToan)
            .Select(h => new HoaDonItem
            {
                LoaiPhi       = h.LoaiPhi,
                KyThanhToan   = h.KyThanhToan,
                SoTien        = h.SoTien,
                NgayThanhToan = h.NgayThanhToan,
                GhiChu        = h.GhiChu
            })
            .ToListAsync();

        return Page();
    }
}
