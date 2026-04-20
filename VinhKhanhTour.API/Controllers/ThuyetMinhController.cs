using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.API.Controllers;

[ApiController]
[Route("api/thuyet-minh")]
public class ThuyetMinhController(AppDbContext db, ILogger<ThuyetMinhController> logger) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly ILogger<ThuyetMinhController> _logger = logger;

    // GET /api/thuyet-minh/{poiId}?lang=vi
    // App gọi cái này khi vào vùng geofence
    [HttpGet("{poiId}")]
    public async Task<IActionResult> GetByPoi(
        Guid poiId,
        [FromQuery] string lang = "vi",
        CancellationToken cancellationToken = default)
    {
        var langCode = NormalizeLanguageCode(lang);

        try
        {
            var thuyetMinhId = await _db.ThuyetMinhs
                .AsNoTracking()
                .Where(tm => tm.POIId == poiId && tm.TrangThai)
                .OrderBy(tm => tm.ThuTu)
                .Select(tm => (Guid?)tm.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (thuyetMinhId == null)
                return NotFound("Chưa có nội dung thuyết minh");

            // Tim ban dich theo ngon ngu, fallback ve tieng Viet.
            var banDich = await _db.BanDichs
                .AsNoTracking()
                .Where(b => b.ThuyetMinhId == thuyetMinhId.Value &&
                            (b.NgonNgu == langCode || b.NgonNgu == "vi"))
                .OrderByDescending(b => b.NgonNgu == langCode)
                .Select(b => new ThuyetMinhDto(
                    b.NoiDung,
                    b.FileAudio,
                    b.NgonNgu))
                .FirstOrDefaultAsync(cancellationToken);

            if (banDich == null)
                return NotFound("Không có bản dịch");

            return Ok(banDich);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499, "Request was cancelled");
        }
        catch (ObjectDisposedException ex) when (IsDisposedThreadWaitHandle(ex))
        {
            _logger.LogWarning(ex, "Database query was interrupted while loading narration for POI {PoiId}", poiId);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Ket noi database bi gian doan. Vui long thu lai.");
        }
    }

    private static string NormalizeLanguageCode(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
            return "vi";

        var normalized = lang.Trim().ToLowerInvariant();
        var dashIndex = normalized.IndexOf('-');
        return dashIndex > 0 ? normalized[..dashIndex] : normalized;
    }

    private static bool IsDisposedThreadWaitHandle(ObjectDisposedException ex)
        => string.Equals(ex.ObjectName, "System.Threading.ManualResetEventSlim", StringComparison.Ordinal);

    private sealed record ThuyetMinhDto(string NoiDung, string? FileAudio, string NgonNgu);
}
