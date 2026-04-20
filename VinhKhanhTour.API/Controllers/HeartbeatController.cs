using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;
using VinhKhanhTour.API.Utils;

namespace VinhKhanhTour.API.Controllers;

/// <summary>
/// Theo dõi vị trí và hành trình thực tế của khách du lịch.
///
/// Luồng tracking:
///   1. App gửi POST /api/heartbeat mỗi 15 giây kèm lat/lng + POI đang đứng gần.
///      → Bảng vitrikhach được upsert (1 dòng / thiết bị).
///
///   2. Khi khách bước vào vùng POI và audio bắt đầu phát:
///      App gửi POST /api/heartbeat/visit → ghi nhận vào lichsuphat.
///
///   3. CMS gọi GET /api/heartbeat/active → danh sách thiết bị online
///      kèm POI đang đứng + số điểm đã ghé.
///
///   4. CMS gọi GET /api/heartbeat/history/{maThietBi} → lịch sử POI đã ghé
///      trong phiên hiện tại (4 tiếng gần nhất).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HeartbeatController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<HeartbeatController> _logger;
    private const int OnlineMinutes = 2;
    private const int SessionHours  = 4;   // lịch sử trong 4h gần nhất
    private const int ViewedPoiExperience = 50;
    private const int VisitedPoiExperience = 100;
    private const int ExperiencePerLevel = 500;

    public HeartbeatController(AppDbContext db, ILogger<HeartbeatController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private static (int ExperiencePoints, int Level, int ExperienceInCurrentLevel, int ExperienceToNextLevel)
        CalculateExperience(int viewedPoiCount, int visitedPoiCount)
    {
        var experiencePoints = (viewedPoiCount * ViewedPoiExperience) + (visitedPoiCount * VisitedPoiExperience);
        var level = Math.Max(1, (experiencePoints / ExperiencePerLevel) + 1);
        var experienceInCurrentLevel = experiencePoints % ExperiencePerLevel;

        return (experiencePoints, level, experienceInCurrentLevel, ExperiencePerLevel - experienceInCurrentLevel);
    }

    // -----------------------------------------------------------------------
    // POST /api/heartbeat
    // Gửi heartbeat từ app — upsert vị trí + POI hiện tại.
    // Body: { MaThietBi, Lat, Lng, PoiIdHienTai?, TenPoiHienTai? }
    // -----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> SendHeartbeat([FromBody] HeartbeatRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi))
            return BadRequest(new { message = "MaThietBi không được trống." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);

        var now = DateTime.UtcNow;

        try
        {
            var existing = await _db.VitriKhachs
                .FirstOrDefaultAsync(v => v.MaThietBi == maThietBi, cancellationToken);

            if (existing == null)
            {
                _db.VitriKhachs.Add(new VitriKhach
                {
                    Id               = Guid.NewGuid(),
                    MaThietBi        = maThietBi,
                    Lat              = req.Lat,
                    Lng              = req.Lng,
                    LanCuoiHeartbeat = now,
                    PoiIdHienTai     = req.PoiIdHienTai,
                    TenPoiHienTai    = req.TenPoiHienTai
                });
            }
            else
            {
                existing.Lat              = req.Lat;
                existing.Lng              = req.Lng;
                existing.LanCuoiHeartbeat = now;
                existing.PoiIdHienTai     = req.PoiIdHienTai;
                existing.TenPoiHienTai    = req.TenPoiHienTai;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (IsCancellationOrDisposed(ex))
        {
            _logger.LogWarning(ex, "Bo qua heartbeat do ket noi bi huy hoac dispose cho thiet bi {DeviceId}.", maThietBi);
            return Ok(new { message = "SKIPPED", skipped = true, reason = "disposed", ServerTime = now });
        }

        return Ok(new { message = "OK", ServerTime = now });
    }

    // -----------------------------------------------------------------------
    // POST /api/heartbeat/visit
    // Ghi nhận khách bước vào POI (audio bắt đầu phát).
    // Body: { MaThietBi, PoiId, NgonNgu? }
    // -----------------------------------------------------------------------
    [HttpPost("visit")]
    public async Task<IActionResult> RecordVisit([FromBody] VisitRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi) || req.PoiId == Guid.Empty)
            return BadRequest(new { message = "MaThietBi và PoiId không được trống." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);
        var ngonNgu = LichSuPhatInputNormalizer.NormalizeNgonNgu(req.NgonNgu);

        // Tránh ghi trùng nếu khách đứng lại — chỉ ghi nếu POI này chưa được ghi trong 10 phút gần nhất
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        bool daCo;
        try
        {
            daCo = await _db.LichSuPhats
                .AnyAsync(l => l.MaThietBi == maThietBi
                            && l.POIId == req.PoiId
                            && l.ThoiGian >= cutoff,
                    cancellationToken);
        }
        catch (Exception ex) when (IsCancellationOrDisposed(ex))
        {
            _logger.LogWarning(ex, "Bo qua ghi nhan ghe POI do ket noi bi huy hoac dispose cho thiet bi {DeviceId}.", maThietBi);
            return Ok(new { recorded = false, skipped = true, reason = "disposed" });
        }

        if (!daCo)
        {
            _db.LichSuPhats.Add(new LichSuPhat
            {
                Id          = Guid.NewGuid(),
                MaThietBi   = maThietBi,
                POIId       = req.PoiId,
                ThoiGian    = DateTime.UtcNow,
                NgonNguDung = ngonNgu,
                Nguon       = LichSuPhatInputNormalizer.NormalizeNguon("app-geofence")
            });
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (IsCancellationOrDisposed(ex))
            {
                _logger.LogWarning(ex, "Bo qua luu ghe POI do ket noi bi huy hoac dispose cho thiet bi {DeviceId}.", maThietBi);
                return Ok(new { recorded = false, skipped = true, reason = "disposed" });
            }
        }

        return Ok(new { recorded = !daCo });
    }

    // -----------------------------------------------------------------------
    // POST /api/heartbeat/view
    // Ghi nhận khách mở trang chi tiết POI (xem thông tin).
    // Body: { MaThietBi, PoiId, NgonNgu? }
    // -----------------------------------------------------------------------
    [HttpPost("view")]
    public async Task<IActionResult> RecordView([FromBody] VisitRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi) || req.PoiId == Guid.Empty)
            return BadRequest(new { message = "MaThietBi và PoiId không được trống." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);
        var ngonNgu   = LichSuPhatInputNormalizer.NormalizeNgonNgu(req.NgonNgu);

        // Dedup 5 phút — tránh ghi trùng khi khách bấm xem nhiều lần liên tiếp
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        bool daCo;
        try
        {
            daCo = await _db.LichSuPhats
                .AnyAsync(l => l.MaThietBi == maThietBi
                            && l.POIId == req.PoiId
                            && l.Nguon == "VIEW"
                            && l.ThoiGian >= cutoff,
                    cancellationToken);
        }
        catch (Exception ex) when (IsCancellationOrDisposed(ex))
        {
            _logger.LogWarning(ex, "Bo qua ghi nhan xem POI do ket noi bi huy hoac dispose cho thiet bi {DeviceId}.", maThietBi);
            return Ok(new { recorded = false, skipped = true, reason = "disposed" });
        }

        if (!daCo)
        {
            _db.LichSuPhats.Add(new LichSuPhat
            {
                Id          = Guid.NewGuid(),
                MaThietBi   = maThietBi,
                POIId       = req.PoiId,
                ThoiGian    = DateTime.UtcNow,
                NgonNguDung = ngonNgu,
                Nguon       = "VIEW"
            });
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (IsCancellationOrDisposed(ex))
            {
                _logger.LogWarning(ex, "Bo qua luu xem POI do ket noi bi huy hoac dispose cho thiet bi {DeviceId}.", maThietBi);
                return Ok(new { recorded = false, skipped = true, reason = "disposed" });
            }
        }

        return Ok(new { recorded = !daCo });
    }

    // -----------------------------------------------------------------------
    // POST /api/heartbeat/sync-history
    // Dong bo lai so POI da xem/da ghe tu app len server
    // de CMS khong bi lech neu mot vai request fire-and-forget bi rot.
    // -----------------------------------------------------------------------
    [HttpPost("sync-history")]
    public async Task<IActionResult> SyncHistory([FromBody] HistorySyncRequest req, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi))
            return BadRequest(new { message = "MaThietBi không được trống." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);
        var ngonNgu = LichSuPhatInputNormalizer.NormalizeNgonNgu(req.NgonNgu);

        var viewedPoiIds = (req.ViewedPoiIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();

        var visitedPoiIds = (req.VisitedPoiIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToHashSet();

        var allPoiIds = viewedPoiIds.Union(visitedPoiIds).ToList();
        if (allPoiIds.Count == 0)
            return Ok(new { insertedViews = 0, insertedVisits = 0 });

        List<HistoryLogRow> existingLogs;
        try
        {
            existingLogs = await _db.LichSuPhats
                .AsNoTracking()
                .Where(l => l.MaThietBi == maThietBi
                         && l.POIId.HasValue
                         && allPoiIds.Contains(l.POIId.Value)
                         && (l.Nguon == "VIEW" || l.Nguon == "GPS"))
                .Select(l => new HistoryLogRow(
                    l.POIId!.Value,
                    l.Nguon))
                .ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Request bi huy khi dong bo lich su cho thiet bi {DeviceId}.", maThietBi);
            return Ok(CreateSkippedHistorySyncResult("cancelled"));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "Ket noi bi dispose khi dong bo lich su cho thiet bi {DeviceId}.", maThietBi);
            return Ok(CreateSkippedHistorySyncResult("disposed"));
        }
        catch (Exception ex) when (IsCancellationOrDisposed(ex))
        {
            _logger.LogWarning(ex, "Ket noi bi huy hoac dispose khi dong bo lich su cho thiet bi {DeviceId}.", maThietBi);
            return Ok(CreateSkippedHistorySyncResult("disposed"));
        }

        var existingViewed = existingLogs
            .Where(x => x.Nguon == "VIEW")
            .Select(x => x.PoiId)
            .ToHashSet();

        var existingVisited = existingLogs
            .Where(x => x.Nguon == "GPS")
            .Select(x => x.PoiId)
            .ToHashSet();

        var now = DateTime.UtcNow;
        var insertedViews = 0;
        var insertedVisits = 0;

        foreach (var poiId in viewedPoiIds.Except(existingViewed))
        {
            _db.LichSuPhats.Add(new LichSuPhat
            {
                Id = Guid.NewGuid(),
                MaThietBi = maThietBi,
                POIId = poiId,
                ThoiGian = now,
                NgonNguDung = ngonNgu,
                Nguon = "VIEW"
            });
            insertedViews++;
        }

        foreach (var poiId in visitedPoiIds.Except(existingVisited))
        {
            _db.LichSuPhats.Add(new LichSuPhat
            {
                Id = Guid.NewGuid(),
                MaThietBi = maThietBi,
                POIId = poiId,
                ThoiGian = now,
                NgonNguDung = ngonNgu,
                Nguon = "GPS"
            });
            insertedVisits++;
        }

        if (insertedViews > 0 || insertedVisits > 0)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Request bi huy khi luu lich su dong bo cho thiet bi {DeviceId}.", maThietBi);
                return Ok(CreateSkippedHistorySyncResult("cancelled"));
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "Ket noi bi dispose khi luu lich su dong bo cho thiet bi {DeviceId}.", maThietBi);
                return Ok(CreateSkippedHistorySyncResult("disposed"));
            }
            catch (Exception ex) when (IsCancellationOrDisposed(ex))
            {
                _logger.LogWarning(ex, "Ket noi bi huy hoac dispose khi luu lich su dong bo cho thiet bi {DeviceId}.", maThietBi);
                return Ok(CreateSkippedHistorySyncResult("disposed"));
            }
        }

        return Ok(new
        {
            insertedViews,
            insertedVisits
        });
    }

    private static object CreateSkippedHistorySyncResult(string reason)
        => new
        {
            insertedViews = 0,
            insertedVisits = 0,
            skipped = true,
            reason
        };

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

    private static object CreateEmptyExperienceProfile(string deviceId)
        => new
        {
            MaThietBi = deviceId,
            ViewedPoiIds = Array.Empty<Guid>(),
            VisitedPoiIds = Array.Empty<Guid>(),
            ViewedPoiCount = 0,
            VisitedPoiCount = 0,
            ExperiencePoints = 0,
            Level = 1,
            ExperienceInCurrentLevel = 0,
            ExperienceToNextLevel = ExperiencePerLevel,
            ExperiencePerLevel,
            LastActivityAt = (DateTime?)null
        };

    private sealed record HistoryLogRow(Guid PoiId, string? Nguon);
    private sealed record ExperienceLogRow(Guid PoiId, string? Nguon, DateTime ThoiGian);

    // -----------------------------------------------------------------------
    // GET /api/heartbeat/profile/{maThietBi}
    // Tra ve lich su da dong bo de app/CMS khoi phuc kinh nghiem sau khi cai lai.
    // -----------------------------------------------------------------------
    [HttpGet("profile/{maThietBi}")]
    public async Task<IActionResult> GetExperienceProfile(string maThietBi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(maThietBi))
            return BadRequest(new { message = "MaThietBi không được trống." });

        var normalizedDeviceId = LichSuPhatInputNormalizer.NormalizeMaThietBi(maThietBi);
        List<ExperienceLogRow> logs;
        try
        {
            logs = await _db.LichSuPhats
                .AsNoTracking()
                .Where(l => l.MaThietBi == normalizedDeviceId
                         && l.POIId.HasValue
                         && (l.Nguon == "VIEW" || l.Nguon == "GPS"))
                .Select(l => new ExperienceLogRow(
                    l.POIId!.Value,
                    l.Nguon,
                    l.ThoiGian))
                .ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Request bi huy khi tai profile kinh nghiem cho thiet bi {DeviceId}.", normalizedDeviceId);
            return Ok(CreateEmptyExperienceProfile(normalizedDeviceId));
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "Ket noi bi dispose khi tai profile kinh nghiem cho thiet bi {DeviceId}.", normalizedDeviceId);
            return Ok(CreateEmptyExperienceProfile(normalizedDeviceId));
        }
        catch (Exception ex) when (IsCancellationOrDisposed(ex))
        {
            _logger.LogWarning(ex, "Ket noi bi huy hoac dispose khi tai profile kinh nghiem cho thiet bi {DeviceId}.", normalizedDeviceId);
            return Ok(CreateEmptyExperienceProfile(normalizedDeviceId));
        }

        var viewedPoiIds = logs
            .Where(l => l.Nguon == "VIEW")
            .Select(l => l.PoiId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var visitedPoiIds = logs
            .Where(l => l.Nguon == "GPS")
            .Select(l => l.PoiId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var xp = CalculateExperience(viewedPoiIds.Count, visitedPoiIds.Count);

        return Ok(new
        {
            MaThietBi = normalizedDeviceId,
            ViewedPoiIds = viewedPoiIds,
            VisitedPoiIds = visitedPoiIds,
            ViewedPoiCount = viewedPoiIds.Count,
            VisitedPoiCount = visitedPoiIds.Count,
            xp.ExperiencePoints,
            xp.Level,
            xp.ExperienceInCurrentLevel,
            xp.ExperienceToNextLevel,
            ExperiencePerLevel,
            LastActivityAt = logs.Count == 0 ? (DateTime?)null : logs.Max(l => l.ThoiGian)
        });
    }

    // -----------------------------------------------------------------------
    // GET /api/heartbeat/active
    // Danh sách thiết bị online kèm POI hiện tại, số quán đã ghé/xem,
    // thời hạn gói đăng ký còn lại.
    // -----------------------------------------------------------------------
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var cutoffOnline  = DateTime.UtcNow.AddMinutes(-OnlineMinutes);
        var cutoffSession = DateTime.UtcNow.AddHours(-SessionHours);

        // Lấy thiết bị online
        var activeDevices = await _db.VitriKhachs
            .AsNoTracking()
            .Where(v => v.LanCuoiHeartbeat >= cutoffOnline)
            .OrderByDescending(v => v.LanCuoiHeartbeat)
            .ToListAsync();

        if (activeDevices.Count == 0)
            return Ok(new { Count = 0, Items = Array.Empty<object>() });

        var deviceIds = activeDevices.Select(v => v.MaThietBi).ToList();
        var now       = DateTime.UtcNow;

        // ── Đếm số POI đã ghé (GPS) trong phiên ───────────────────────
        var gheCounts = await _db.LichSuPhats
            .AsNoTracking()
            .Where(l => l.MaThietBi != null
                     && deviceIds.Contains(l.MaThietBi!)
                     && l.Nguon == "GPS"
                     && l.ThoiGian >= cutoffSession)
            .GroupBy(l => l.MaThietBi!)
            .Select(g => new { MaThietBi = g.Key, Count = g.Select(l => l.POIId).Distinct().Count() })
            .ToListAsync();

        var gheMap = gheCounts.ToDictionary(x => x.MaThietBi, x => x.Count);

        // ── Đếm số POI đã xem (chi tiết) trong phiên ──────────────────
        var xemCounts = await _db.LichSuPhats
            .AsNoTracking()
            .Where(l => l.MaThietBi != null
                     && deviceIds.Contains(l.MaThietBi!)
                     && l.Nguon == "VIEW"
                     && l.ThoiGian >= cutoffSession)
            .GroupBy(l => l.MaThietBi!)
            .Select(g => new { MaThietBi = g.Key, Count = g.Select(l => l.POIId).Distinct().Count() })
            .ToListAsync();

        var xemMap = xemCounts.ToDictionary(x => x.MaThietBi, x => x.Count);

        // ── Gói đăng ký còn hiệu lực (lấy gói hết hạn muộn nhất) ──────
        var subs = await _db.DangKyApps
            .AsNoTracking()
            .Where(d => deviceIds.Contains(d.MaThietBi))
            .ToListAsync();

        var subMap = subs
            .GroupBy(d => d.MaThietBi)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.NgayHetHan).First().NgayHetHan);

        // ── Tổng hợp ───────────────────────────────────────────────────
        var items = activeDevices.Select(v =>
        {
            var soGhe   = gheMap.TryGetValue(v.MaThietBi, out int gc) ? gc : 0;
            var soXem   = xemMap.TryGetValue(v.MaThietBi, out int xc) ? xc : 0;
            var hasHan  = subMap.TryGetValue(v.MaThietBi, out DateTime hh) && hh > now;
            var xp = CalculateExperience(soXem, soGhe);
            return new
            {
                v.Lat,
                v.Lng,
                DeviceShort   = v.MaThietBi[..Math.Min(8, v.MaThietBi.Length)].ToUpper(),
                LanCuoi       = v.LanCuoiHeartbeat,
                PoiIdHienTai  = v.PoiIdHienTai,
                TenPoiHienTai = v.TenPoiHienTai ?? "Đang di chuyển",
                SoQuanDaGhe   = soGhe,
                SoQuanDaXem   = soXem,
                xp.ExperiencePoints,
                xp.Level,
                xp.ExperienceInCurrentLevel,
                xp.ExperienceToNextLevel,
                ExperiencePerLevel,
                NgayHetHan    = hasHan ? hh : (DateTime?)null,
                ConLaiNgay    = hasHan ? (int?)Math.Max(1, (int)(hh - now).TotalDays + 1) : null
            };
        }).ToList();

        return Ok(new { Count = items.Count, Items = items });
    }

    // -----------------------------------------------------------------------
    // GET /api/heartbeat/history/{deviceShort}
    // Lịch sử POI đã ghé trong phiên hiện tại — dùng cho popup CMS.
    // deviceShort = 6 ký tự đầu viết hoa của MaThietBi
    // -----------------------------------------------------------------------
    [HttpGet("history/{deviceShort}")]
    public async Task<IActionResult> GetHistory(string deviceShort)
    {
        var cutoff = DateTime.UtcNow.AddHours(-SessionHours);

        // Tìm MaThietBi đầy đủ từ short prefix
        var device = await _db.VitriKhachs
            .AsNoTracking()
            .Where(v => v.MaThietBi.ToUpper().StartsWith(deviceShort.ToUpper()))
            .FirstOrDefaultAsync();

        if (device == null) return NotFound();

        var history = await _db.LichSuPhats
            .AsNoTracking()
            .Where(l => l.MaThietBi == device.MaThietBi && l.ThoiGian >= cutoff)
            .OrderByDescending(l => l.ThoiGian)
            .Join(_db.POIs, l => l.POIId, p => p.Id, (l, p) => new
            {
                TenPOI   = p.TenPOI,
                DiaChi   = p.DiaChi,
                ThoiGian = l.ThoiGian,
                Lat      = p.ViDo,
                Lng      = p.KinhDo
            })
            .ToListAsync();

        return Ok(new
        {
            DeviceShort = deviceShort.ToUpper(),
            SoDiemDaGhe = history.Select(h => h.TenPOI).Distinct().Count(),
            LichSu = history
        });
    }
}

public class HeartbeatRequest
{
    public string  MaThietBi     { get; set; } = "";
    public double  Lat           { get; set; }
    public double  Lng           { get; set; }
    public Guid?   PoiIdHienTai  { get; set; }
    public string? TenPoiHienTai { get; set; }
}

public class VisitRequest
{
    public string MaThietBi { get; set; } = "";
    public Guid   PoiId     { get; set; }
    public string? NgonNgu  { get; set; }
}

public class HistorySyncRequest
{
    public string MaThietBi { get; set; } = "";
    public List<Guid>? ViewedPoiIds { get; set; }
    public List<Guid>? VisitedPoiIds { get; set; }
    public string? NgonNgu { get; set; }
}
