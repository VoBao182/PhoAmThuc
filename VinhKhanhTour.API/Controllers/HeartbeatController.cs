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
    private const int OnlineMinutes = 2;
    private const int SessionHours  = 4;   // lịch sử trong 4h gần nhất

    public HeartbeatController(AppDbContext db) => _db = db;

    // -----------------------------------------------------------------------
    // POST /api/heartbeat
    // Gửi heartbeat từ app — upsert vị trí + POI hiện tại.
    // Body: { MaThietBi, Lat, Lng, PoiIdHienTai?, TenPoiHienTai? }
    // -----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> SendHeartbeat([FromBody] HeartbeatRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi))
            return BadRequest(new { message = "MaThietBi không được trống." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);

        var now = DateTime.UtcNow;

        var existing = await _db.VitriKhachs
            .FirstOrDefaultAsync(v => v.MaThietBi == maThietBi);

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

        await _db.SaveChangesAsync();
        return Ok(new { message = "OK", ServerTime = now });
    }

    // -----------------------------------------------------------------------
    // POST /api/heartbeat/visit
    // Ghi nhận khách bước vào POI (audio bắt đầu phát).
    // Body: { MaThietBi, PoiId, NgonNgu? }
    // -----------------------------------------------------------------------
    [HttpPost("visit")]
    public async Task<IActionResult> RecordVisit([FromBody] VisitRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi) || req.PoiId == Guid.Empty)
            return BadRequest(new { message = "MaThietBi và PoiId không được trống." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);
        var ngonNgu = LichSuPhatInputNormalizer.NormalizeNgonNgu(req.NgonNgu);

        // Tránh ghi trùng nếu khách đứng lại — chỉ ghi nếu POI này chưa được ghi trong 10 phút gần nhất
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        bool daCo = await _db.LichSuPhats
            .AnyAsync(l => l.MaThietBi == maThietBi
                        && l.POIId == req.PoiId
                        && l.ThoiGian >= cutoff);

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
            await _db.SaveChangesAsync();
        }

        return Ok(new { recorded = !daCo });
    }

    // -----------------------------------------------------------------------
    // POST /api/heartbeat/view
    // Ghi nhận khách mở trang chi tiết POI (xem thông tin).
    // Body: { MaThietBi, PoiId, NgonNgu? }
    // -----------------------------------------------------------------------
    [HttpPost("view")]
    public async Task<IActionResult> RecordView([FromBody] VisitRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi) || req.PoiId == Guid.Empty)
            return BadRequest(new { message = "MaThietBi và PoiId không được trống." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);
        var ngonNgu   = LichSuPhatInputNormalizer.NormalizeNgonNgu(req.NgonNgu);

        // Dedup 5 phút — tránh ghi trùng khi khách bấm xem nhiều lần liên tiếp
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        bool daCo = await _db.LichSuPhats
            .AnyAsync(l => l.MaThietBi == maThietBi
                        && l.POIId == req.PoiId
                        && l.Nguon == "VIEW"
                        && l.ThoiGian >= cutoff);

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
            await _db.SaveChangesAsync();
        }

        return Ok(new { recorded = !daCo });
    }

    // -----------------------------------------------------------------------
    // POST /api/heartbeat/sync-history
    // Dong bo lai so POI da xem/da ghe tu app len server
    // de CMS khong bi lech neu mot vai request fire-and-forget bi rot.
    // -----------------------------------------------------------------------
    [HttpPost("sync-history")]
    public async Task<IActionResult> SyncHistory([FromBody] HistorySyncRequest req)
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

        var existingLogs = await _db.LichSuPhats
            .AsNoTracking()
            .Where(l => l.MaThietBi == maThietBi
                     && l.POIId.HasValue
                     && allPoiIds.Contains(l.POIId.Value)
                     && (l.Nguon == "VIEW" || l.Nguon == "GPS"))
            .Select(l => new
            {
                PoiId = l.POIId!.Value,
                l.Nguon
            })
            .ToListAsync();

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
            await _db.SaveChangesAsync();

        return Ok(new
        {
            insertedViews,
            insertedVisits
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
