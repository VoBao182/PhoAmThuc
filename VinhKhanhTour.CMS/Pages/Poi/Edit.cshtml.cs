using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.Poi;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    public EditModel(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5118";

    [BindProperty] public POI POI { get; set; } = null!;

    // Thuyết minh 3 ngôn ngữ — bind thủ công vì nested phức tạp
    [BindProperty] public string? ThuyetMinhVi { get; set; }
    [BindProperty] public string? ThuyetMinhEn { get; set; }
    [BindProperty] public string? ThuyetMinhZh { get; set; }

    // Danh sách món ăn từ form
    [BindProperty] public List<MonAnInput> MonAns { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var poi = await _db.POIs
            .Include(p => p.MonAns.Where(m => m.TinhTrang))
            .Include(p => p.ThuyetMinhs.Where(t => t.TrangThai))
                .ThenInclude(t => t.BanDichs)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poi == null) return NotFound();
        POI = poi;

        // Load thuyết minh theo ngôn ngữ
        var tm = poi.ThuyetMinhs.FirstOrDefault();
        if (tm != null)
        {
            ThuyetMinhVi = tm.BanDichs.FirstOrDefault(b => b.NgonNgu == "vi")?.NoiDung;
            ThuyetMinhEn = tm.BanDichs.FirstOrDefault(b => b.NgonNgu == "en")?.NoiDung;
            ThuyetMinhZh = tm.BanDichs.FirstOrDefault(b => b.NgonNgu == "zh")?.NoiDung;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            // reload MonAns for display
            var reloaded = await _db.POIs
                .Include(p => p.MonAns.Where(m => m.TinhTrang))
                .FirstOrDefaultAsync(p => p.Id == POI.Id);
            POI.MonAns = reloaded?.MonAns.ToList() ?? [];
            return Page();
        }

        // ── 1. Cập nhật POI ──
        var poi = await _db.POIs
            .Include(p => p.MonAns)
            .Include(p => p.ThuyetMinhs.Where(t => t.TrangThai))
                .ThenInclude(t => t.BanDichs)
            .FirstOrDefaultAsync(p => p.Id == POI.Id);

        if (poi == null) return NotFound();

        poi.TenPOI     = POI.TenPOI;
        poi.DiaChi     = POI.DiaChi;
        poi.SDT        = POI.SDT;
        poi.AnhDaiDien = POI.AnhDaiDien;
        poi.ViDo       = POI.ViDo;
        poi.KinhDo     = POI.KinhDo;
        poi.BanKinh    = POI.BanKinh;
        poi.MucUuTien  = POI.MucUuTien;
        poi.TrangThai  = POI.TrangThai;

        // ── 2. Thuyết minh + bản dịch ──
        var tm = poi.ThuyetMinhs.FirstOrDefault();
        if (tm == null)
        {
            tm = new VinhKhanhTour.API.Models.ThuyetMinh
            {
                Id        = Guid.NewGuid(),
                POIId     = poi.Id,
                TrangThai = true
            };
            poi.ThuyetMinhs.Add(tm);
        }

        UpsertBanDich(tm, "vi", ThuyetMinhVi);
        UpsertBanDich(tm, "en", ThuyetMinhEn);
        UpsertBanDich(tm, "zh", ThuyetMinhZh);

        // ── 3. Món ăn ──
        // Ẩn món không còn trong form
        var formIds = MonAns
            .Where(m => m.Id.HasValue && m.Id.Value != Guid.Empty)
            .Select(m => m.Id!.Value)
            .ToHashSet();

        foreach (var existing in poi.MonAns)
        {
            if (!formIds.Contains(existing.Id))
                existing.TinhTrang = false;
        }

        foreach (var input in MonAns)
        {
            if (string.IsNullOrWhiteSpace(input.TenMonAn)) continue;

            if (!input.Id.HasValue || input.Id.Value == Guid.Empty)
            {
                // Thêm mới
                poi.MonAns.Add(new MonAn
                {
                    Id        = Guid.NewGuid(),
                    POIId     = poi.Id,
                    TenMonAn  = input.TenMonAn,
                    MoTa      = input.MoTa,
                    HinhAnh   = input.HinhAnh,
                    PhanLoai  = input.PhanLoai,
                    DonGia    = input.DonGia ?? 0m,
                    TinhTrang = true
                });
            }
            else
            {
                // Cập nhật
                var mon = poi.MonAns.FirstOrDefault(m => m.Id == input.Id!.Value);
                if (mon == null) continue;
                mon.TenMonAn  = input.TenMonAn;
                mon.MoTa      = input.MoTa;
                mon.HinhAnh   = input.HinhAnh;
                mon.PhanLoai  = input.PhanLoai;
                mon.DonGia    = input.DonGia ?? 0m;
                mon.TinhTrang = true;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Đã lưu thông tin quán \"{poi.TenPOI}\"";
            return RedirectToPage(new { id = poi.Id });
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Dữ liệu đã bị thay đổi bởi tiến trình khác. Vui lòng tải lại trang và thử lại.";
            return RedirectToPage(new { id = poi.Id });
        }
    }

    private void UpsertBanDich(VinhKhanhTour.API.Models.ThuyetMinh tm, string lang, string? noiDung)
    {
        var bd = tm.BanDichs.FirstOrDefault(b => b.NgonNgu == lang);
        if (bd == null)
        {
            if (string.IsNullOrWhiteSpace(noiDung)) return;
            tm.BanDichs.Add(new BanDich
            {
                Id         = Guid.NewGuid(),
                ThuyetMinhId = tm.Id,
                NgonNgu    = lang,
                NoiDung    = noiDung
            });
        }
        else
        {
            bd.NoiDung = noiDung ?? "";
        }
    }
}

// DTO nhận từ form (tránh conflict với Model MonAn)
public class MonAnInput
{
    public Guid?  Id        { get; set; }
    public string TenMonAn  { get; set; } = "";
    public string? MoTa     { get; set; }
    public string? HinhAnh  { get; set; }
    public string? PhanLoai { get; set; }
    public decimal? DonGia  { get; set; }
    public bool TinhTrang   { get; set; } = true;
}
