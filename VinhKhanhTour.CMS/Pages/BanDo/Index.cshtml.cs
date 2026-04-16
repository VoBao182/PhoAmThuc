using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.BanDo;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public IndexModel(AppDbContext db, IConfiguration config)
    {
        _db    = db;
        _config = config;
    }

    public string ApiBaseUrl { get; set; } = "";
    public string PoisJson   { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        // URL của API — dùng chính connection string nếu CMS chạy local
        ApiBaseUrl = _config["ApiBaseUrl"] ?? "http://localhost:5000";

        var pois = await _db.POIs.AsNoTracking()
            .Where(p => p.TrangThai)
            .OrderBy(p => p.TenPOI)
            .Select(p => new { lat = p.ViDo, lng = p.KinhDo, ten = p.TenPOI, diaChi = p.DiaChi })
            .ToListAsync();

        PoisJson = JsonSerializer.Serialize(pois, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
