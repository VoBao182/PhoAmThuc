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
    public string Search { get; private set; } = "";
    public string StatusFilter { get; private set; } = "all";
    public string ExpiryFilter { get; private set; } = "all";
    public string SortBy { get; private set; } = "priority";
    public string SortDir { get; private set; } = "asc";

    public async Task OnGetAsync(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? expiry,
        [FromQuery] string? sort,
        [FromQuery] string? dir)
    {
        Search = (search ?? "").Trim();
        StatusFilter = NormalizeOption(status, "all");
        ExpiryFilter = NormalizeOption(expiry, "all");
        SortBy = NormalizeOption(sort, "priority");
        SortDir = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

        try
        {
            var query = _db.POIs
                .AsNoTracking()
                .Include(p => p.MonAns)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                query = query.Where(p =>
                    p.TenPOI.Contains(Search) ||
                    (p.DiaChi != null && p.DiaChi.Contains(Search)) ||
                    (p.SDT != null && p.SDT.Contains(Search)));
            }

            var pois = await query.ToListAsync();
            var now = DateTime.UtcNow;
            POIs = ApplySort(ApplyExpiryFilter(ApplyStatusFilter(pois, now), now), now).ToList();
        }
        catch (Exception ex)
        {
            POIs = [];
            ErrorMessage = $"Không thể tải danh sách POI: {ex.GetBaseException().Message}";
        }
    }

    private static string NormalizeOption(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private IEnumerable<POI> ApplyStatusFilter(IEnumerable<POI> pois, DateTime now)
        => StatusFilter switch
        {
            "visible" => pois.Where(p => p.TrangThai),
            "hidden" => pois.Where(p => !p.TrangThai),
            "active-sub" => pois.Where(p => p.NgayHetHanDuyTri.HasValue && p.NgayHetHanDuyTri.Value >= now),
            "expired" => pois.Where(p => p.NgayHetHanDuyTri.HasValue && p.NgayHetHanDuyTri.Value < now),
            "no-expiry" => pois.Where(p => !p.NgayHetHanDuyTri.HasValue),
            _ => pois
        };

    private IEnumerable<POI> ApplyExpiryFilter(IEnumerable<POI> pois, DateTime now)
        => ExpiryFilter switch
        {
            "active" => pois.Where(p => p.NgayHetHanDuyTri.HasValue && p.NgayHetHanDuyTri.Value >= now),
            "expiring7" => pois.Where(p => p.NgayHetHanDuyTri.HasValue && p.NgayHetHanDuyTri.Value >= now && p.NgayHetHanDuyTri.Value <= now.AddDays(7)),
            "expired" => pois.Where(p => p.NgayHetHanDuyTri.HasValue && p.NgayHetHanDuyTri.Value < now),
            "none" => pois.Where(p => !p.NgayHetHanDuyTri.HasValue),
            _ => pois
        };

    private IEnumerable<POI> ApplySort(IEnumerable<POI> pois, DateTime now)
    {
        var descending = SortDir == "desc";

        IOrderedEnumerable<POI> ordered = SortBy switch
        {
            "name" => descending
                ? pois.OrderByDescending(p => p.TenPOI, StringComparer.CurrentCultureIgnoreCase)
                : pois.OrderBy(p => p.TenPOI, StringComparer.CurrentCultureIgnoreCase),
            "expiry" => descending
                ? pois.OrderByDescending(p => GetRemainingDays(p, now))
                : pois.OrderBy(p => GetRemainingDays(p, now)),
            "menu" => descending
                ? pois.OrderByDescending(p => p.MonAns.Count(m => m.TinhTrang))
                : pois.OrderBy(p => p.MonAns.Count(m => m.TinhTrang)),
            "status" => descending
                ? pois.OrderByDescending(p => p.TrangThai)
                : pois.OrderBy(p => p.TrangThai),
            _ => descending
                ? pois.OrderByDescending(p => p.MucUuTien)
                : pois.OrderBy(p => p.MucUuTien)
        };

        return ordered.ThenBy(p => p.TenPOI, StringComparer.CurrentCultureIgnoreCase);
    }

    public static int GetRemainingDays(POI poi)
        => GetRemainingDays(poi, DateTime.UtcNow);

    private static int GetRemainingDays(POI poi, DateTime now)
    {
        if (!poi.NgayHetHanDuyTri.HasValue)
            return int.MinValue;

        return (int)Math.Floor((poi.NgayHetHanDuyTri.Value - now).TotalDays);
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
