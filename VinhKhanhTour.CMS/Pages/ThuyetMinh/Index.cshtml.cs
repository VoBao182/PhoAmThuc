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
    public string? LoiMsg { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var pois = await _db.POIs
                .AsNoTracking()
                .OrderBy(p => p.MucUuTien)
                .Select(p => new
                {
                    p.Id,
                    p.TenPOI,
                    p.TrangThai
                })
                .ToListAsync();

            var banDichs = await _db.ThuyetMinhs
                .AsNoTracking()
                .Where(t => t.TrangThai)
                .SelectMany(t => t.BanDichs.Select(b => new
                {
                    t.POIId,
                    b.NgonNgu,
                    b.NoiDung
                }))
                .Where(b => !string.IsNullOrWhiteSpace(b.NoiDung))
                .ToListAsync();

            var banDichByPoi = banDichs
                .GroupBy(b => b.POIId)
                .ToDictionary(g => g.Key, g => g.ToList());

            Items = pois.Select(p =>
            {
                banDichByPoi.TryGetValue(p.Id, out var translations);
                translations ??= [];

                return new ThuyetMinhRow
                {
                    PoiId = p.Id,
                    TenPOI = p.TenPOI,
                    TrangThaiPoi = p.TrangThai,
                    SoBanDich = translations.Count,
                    NoiDungVi = translations.FirstOrDefault(b => b.NgonNgu == "vi")?.NoiDung,
                    NoiDungEn = translations.FirstOrDefault(b => b.NgonNgu == "en")?.NoiDung,
                    NoiDungZh = translations.FirstOrDefault(b => b.NgonNgu == "zh")?.NoiDung
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            Items = [];
            LoiMsg = $"Không thể tải danh sách thuyết minh: {ex.GetBaseException().Message}";
        }
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
