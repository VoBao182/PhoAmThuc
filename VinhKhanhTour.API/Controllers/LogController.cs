using Microsoft.AspNetCore.Mvc;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;
using VinhKhanhTour.API.Utils;

namespace VinhKhanhTour.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogController : ControllerBase
{
    private readonly AppDbContext _db;
    public LogController(AppDbContext db) => _db = db;

    // POST /api/log — ghi lịch sử phát từ app
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LogRequest req)
    {
        try
        {
            var log = new LichSuPhat
            {
                Id           = Guid.NewGuid(),
                MaThietBi    = string.IsNullOrWhiteSpace(req.MaThietBi)
                    ? null
                    : LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi),
                POIId        = req.POIId,
                NgonNguDung  = LichSuPhatInputNormalizer.NormalizeNgonNgu(req.NgonNguDung),
                ThoiGian     = req.ThoiGian ?? DateTime.UtcNow,
                Nguon        = LichSuPhatInputNormalizer.NormalizeNguon(req.Nguon)
            };

            _db.LichSuPhats.Add(log);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, id = log.Id });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Log] Lỗi ghi: {ex.Message}");
            return StatusCode(500, new { success = false });
        }
    }
}

public class LogRequest
{
    public string?   MaThietBi   { get; set; }
    public Guid?     POIId       { get; set; }
    public string?   NgonNguDung { get; set; }
    public DateTime? ThoiGian    { get; set; }
    public string?   Nguon       { get; set; }
}
