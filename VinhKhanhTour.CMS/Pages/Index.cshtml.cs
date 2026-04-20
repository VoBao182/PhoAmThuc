using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<IndexModel> _logger;
    public IndexModel(AppDbContext db, IConfiguration config, ILogger<IndexModel> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public List<POI> POIs        { get; set; } = [];
    public int TongPOI           { get; set; }
    public int TongMonAn         { get; set; }
    public int SoQuanQuaHan      { get; set; }
    public string? ErrorMessage  { get; private set; }
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

            var now = DateTime.UtcNow;
            TongPOI      = POIs.Count(p => p.TrangThai);
            TongMonAn    = POIs.SelectMany(p => p.MonAns).Count(m => m.TinhTrang);
            SoQuanQuaHan = POIs.Count(p =>
                p.TrangThai && (p.NgayHetHanDuyTri == null || p.NgayHetHanDuyTri < now));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load POIs from database on CMS home page");
            POIs = [];
            TongPOI = 0;
            TongMonAn = 0;
            SoQuanQuaHan = 0;
            ErrorMessage = $"Không thể tải dữ liệu từ database: {ex.GetBaseException().Message}";
        }
    }
}
