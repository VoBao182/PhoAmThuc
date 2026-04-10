using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.ThuyetMinh;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public List<ThuyetMinhRow> Items { get; set; } = [];

    public async Task OnGetAsync()
    {
        Items = await _db.POIs
            .Include(p => p.ThuyetMinhs.Where(t => t.TrangThai))
                .ThenInclude(t => t.BanDichs)
            .OrderBy(p => p.MucUuTien)
            .Select(p => new ThuyetMinhRow
            {
                PoiId = p.Id,
                TenPOI = p.TenPOI,
                TrangThaiPoi = p.TrangThai,
                SoBanDich = p.ThuyetMinhs
                    .SelectMany(t => t.BanDichs)
                    .Count(b => !string.IsNullOrWhiteSpace(b.NoiDung)),
                NoiDungVi = p.ThuyetMinhs
                    .SelectMany(t => t.BanDichs)
                    .Where(b => b.NgonNgu == "vi")
                    .Select(b => b.NoiDung)
                    .FirstOrDefault(),
                NoiDungEn = p.ThuyetMinhs
                    .SelectMany(t => t.BanDichs)
                    .Where(b => b.NgonNgu == "en")
                    .Select(b => b.NoiDung)
                    .FirstOrDefault(),
                NoiDungZh = p.ThuyetMinhs
                    .SelectMany(t => t.BanDichs)
                    .Where(b => b.NgonNgu == "zh")
                    .Select(b => b.NoiDung)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    public sealed class ThuyetMinhRow
    {
        public Guid PoiId { get; set; }
        public string TenPOI { get; set; } = "";
        public bool TrangThaiPoi { get; set; }
        public int SoBanDich { get; set; }
        public string? NoiDungVi { get; set; }
        public string? NoiDungEn { get; set; }
        public string? NoiDungZh { get; set; }
    }
}
