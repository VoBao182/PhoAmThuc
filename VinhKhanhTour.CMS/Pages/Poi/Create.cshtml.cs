using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.Poi;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    public CreateModel(AppDbContext db, IConfiguration config) { _db = db; _config = config; }

    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5118";

    [BindProperty] public POI POI { get; set; } = new();
    [BindProperty] public string? ThuyetMinhVi { get; set; }
    [BindProperty] public string? ThuyetMinhEn { get; set; }
    [BindProperty] public string? ThuyetMinhZh { get; set; }
    [BindProperty] public List<MonAnInput> MonAns { get; set; } = [];

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        POI.Id        = Guid.NewGuid();
        POI.TrangThai = true;

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
                TenMonAn = m.TenMonAn,
                MoTa     = m.MoTa,
                HinhAnh  = m.HinhAnh,
                PhanLoai = m.PhanLoai,
                DonGia   = m.DonGia,
                TinhTrang = true
            }).ToList();

        _db.POIs.Add(POI);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã tạo quán \"{POI.TenPOI}\" thành công!";
        return RedirectToPage("/Poi/Index");
    }
}
