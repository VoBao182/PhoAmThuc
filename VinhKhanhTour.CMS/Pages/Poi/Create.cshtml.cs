using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.Poi;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(AppDbContext db, IConfiguration config, ILogger<CreateModel> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5118";

    [BindProperty] public POI POI { get; set; } = new()
    {
        TrangThai = true,
        BanKinh = 10,
        MucUuTien = 1
    };
    [BindProperty] public string? ThuyetMinhVi { get; set; }
    [BindProperty] public string? ThuyetMinhEn { get; set; }
    [BindProperty] public string? ThuyetMinhZh { get; set; }
    [BindProperty] public List<MonAnInput> MonAns { get; set; } = [];

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        POI.Id = Guid.NewGuid();

        // Thuyết minh
        var tm = new VinhKhanhTour.API.Models.ThuyetMinh
        {
            Id        = Guid.NewGuid(),
            POIId     = POI.Id,
            TrangThai = true,
            BanDichs  = []
        };

        if (!string.IsNullOrWhiteSpace(ThuyetMinhVi))
            tm.BanDichs.Add(new BanDich { Id = Guid.NewGuid(), ThuyetMinhId = tm.Id, NgonNgu = "vi", NoiDung = ThuyetMinhVi });
        if (!string.IsNullOrWhiteSpace(ThuyetMinhEn))
            tm.BanDichs.Add(new BanDich { Id = Guid.NewGuid(), ThuyetMinhId = tm.Id, NgonNgu = "en", NoiDung = ThuyetMinhEn });
        if (!string.IsNullOrWhiteSpace(ThuyetMinhZh))
            tm.BanDichs.Add(new BanDich { Id = Guid.NewGuid(), ThuyetMinhId = tm.Id, NgonNgu = "zh", NoiDung = ThuyetMinhZh });

        POI.ThuyetMinhs = [tm];

        // Món ăn
        POI.MonAns = MonAns
            .Where(m => !string.IsNullOrWhiteSpace(m.TenMonAn))
            .Select(m => new MonAn
            {
                Id       = Guid.NewGuid(),
                POIId    = POI.Id,
                TenMonAn = m.TenMonAn!,
                MoTa     = m.MoTa,
                HinhAnh  = m.HinhAnh,
                PhanLoai = m.PhanLoai,
                DonGia   = m.DonGia ?? 0m,
                TinhTrang = true
            }).ToList();

        try
        {
            _db.POIs.Add(POI);
            await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã tạo quán \"{POI.TenPOI}\" thành công!";
            return RedirectToPage("/Poi/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create POI {PoiName}", POI.TenPOI);
            TempData["Error"] = "Khong the luu du lieu vi CMS khong ket noi duoc toi database. Vui long kiem tra health/db hoac cap nhat SUPABASE_CONNECTION_STRING.";
            return Page();
        }
    }
}
