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
    private readonly ILogger<LogController> _logger;

    public LogController(AppDbContext db, ILogger<LogController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // POST /api/log — ghi lịch sử phát từ app
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LogRequest req, CancellationToken cancellationToken)
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
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, id = log.Id });
        }
        catch (Exception ex) when (IsCancellationOrDisposed(ex))
        {
            _logger.LogWarning(ex, "Bo qua log lich su phat do ket noi bi huy hoac dispose.");
            return Ok(new { success = false, skipped = true, reason = "disposed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi ghi log lich su phat.");
            return StatusCode(500, new { success = false });
        }
    }

    private static bool IsCancellationOrDisposed(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is OperationCanceledException or ObjectDisposedException)
                return true;
        }

        var baseException = exception.GetBaseException();
        return baseException is OperationCanceledException or ObjectDisposedException;
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
