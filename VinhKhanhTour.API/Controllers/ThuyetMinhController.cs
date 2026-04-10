using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.API.Controllers;

[ApiController]
[Route("api/thuyet-minh")]
public class ThuyetMinhController : ControllerBase
{
    private readonly AppDbContext _db;
    public ThuyetMinhController(AppDbContext db) => _db = db;

    // GET /api/thuyet-minh/{poiId}?lang=vi
    // App gọi cái này khi vào vùng geofence
    [HttpGet("{poiId}")]
    public async Task<IActionResult> GetByPoi(
        Guid poiId, [FromQuery] string lang = "vi")
    {
        var thuyetMinh = await _db.ThuyetMinhs
            .Include(tm => tm.BanDichs)
            .Where(tm => tm.POIId == poiId && tm.TrangThai)
            .OrderBy(tm => tm.ThuTu)
            .FirstOrDefaultAsync();

        if (thuyetMinh == null)
            return NotFound("Chưa có nội dung thuyết minh");

        // Tìm bản dịch theo ngôn ngữ, fallback về tiếng Việt
        var banDich = thuyetMinh.BanDichs
            .FirstOrDefault(b => b.NgonNgu == lang)
            ?? thuyetMinh.BanDichs
            .FirstOrDefault(b => b.NgonNgu == "vi");

        if (banDich == null)
            return NotFound("Không có bản dịch");

        return Ok(new
        {
            NoiDung = banDich.NoiDung,
            FileAudio = banDich.FileAudio,
            NgonNgu = banDich.NgonNgu
        });
    }
}