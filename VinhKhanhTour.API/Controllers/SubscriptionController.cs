using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;
using VinhKhanhTour.API.Utils;

namespace VinhKhanhTour.API.Controllers;

/// <summary>
/// Quản lý gói đăng ký — khách ẩn danh, định danh bằng MaThietBi.
///
/// Gói:
///   "thu"   — dùng thử 3 ngày, miễn phí, mỗi thiết bị chỉ dùng được 1 lần
///   "ngay"  — 1 ngày,  29.000đ
///   "tuan"  — 7 ngày,  99.000đ
///   "thang" — 30 ngày, 199.000đ
///   "nam"   — 365 ngày, 999.000đ
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly AppDbContext _db;

    private static readonly Dictionary<string, (decimal Gia, int SoNgay, string Ten, bool MienPhi)> Goi = new()
    {
        ["thu"]   = (0m,         3,   "Dùng thử 3 ngày", true),
        ["ngay"]  = (29_000m,    1,   "1 ngày",          false),
        ["tuan"]  = (99_000m,    7,   "1 tuần",          false),
        ["thang"] = (199_000m,   30,  "1 tháng",         false),
        ["nam"]   = (999_000m,   365, "1 năm",           false),
    };

    public SubscriptionController(AppDbContext db) => _db = db;

    // GET /api/subscription/plans
    [HttpGet("plans")]
    public IActionResult GetPlans() =>
        Ok(Goi.Select(kv => new
        {
            LoaiGoi = kv.Key,
            Ten     = kv.Value.Ten,
            SoNgay  = kv.Value.SoNgay,
            Gia     = kv.Value.Gia,
            MienPhi = kv.Value.MienPhi
        }));

    // GET /api/subscription/status/{maThietBi}
    [HttpGet("status/{maThietBi}")]
    public async Task<IActionResult> GetStatus(string maThietBi)
    {
        maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(maThietBi);
        if (string.IsNullOrWhiteSpace(maThietBi))
            return BadRequest(new { message = "MaThietBi khÃ´ng Ä‘Æ°á»£c trá»‘ng." });

        var now = DateTime.UtcNow;
        var goi = await _db.DangKyApps
            .AsNoTracking()
            .Where(d => d.MaThietBi == maThietBi && d.NgayHetHan > now)
            .OrderByDescending(d => d.NgayHetHan)
            .FirstOrDefaultAsync();

        // Kiểm tra thiết bị đã từng dùng thử chưa
        bool daDungThu = await _db.DangKyApps
            .AsNoTracking()
            .AnyAsync(d => d.MaThietBi == maThietBi && d.LoaiGoi == "thu");

        if (goi == null)
            return Ok(new
            {
                CoDangKy   = false,
                NgayHetHan = (DateTime?)null,
                SoNgayConLai = 0,
                LoaiGoi    = (string?)null,
                DaDungThu  = daDungThu
            });

        return Ok(new
        {
            CoDangKy     = true,
            NgayHetHan   = goi.NgayHetHan,
            SoNgayConLai = (int)(goi.NgayHetHan - now).TotalDays + 1,
            LoaiGoi      = goi.LoaiGoi,
            DaDungThu    = daDungThu
        });
    }

    // POST /api/subscription/purchase
    // Body: { MaThietBi, LoaiGoi }
    [HttpPost("purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi))
            return BadRequest(new { message = "MaThietBi không được trống." });

        if (!Goi.TryGetValue(req.LoaiGoi, out var info))
            return BadRequest(new { message = "Loại gói không hợp lệ. Chọn: thu, ngay, thang, nam." });

        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);
        if (string.IsNullOrWhiteSpace(maThietBi))
            return BadRequest(new { message = "MaThietBi không hợp lệ." });

        var now = DateTime.UtcNow;

        // Gói dùng thử: mỗi thiết bị chỉ được 1 lần
        if (info.MienPhi)
        {
            bool daDung = await _db.DangKyApps
                .AnyAsync(d => d.MaThietBi == maThietBi && d.LoaiGoi == "thu");
            if (daDung)
                return BadRequest(new { message = "Thiết bị này đã sử dụng gói dùng thử." });
        }

        // Nếu đang có gói chưa hết → gia hạn nối tiếp (chỉ áp dụng gói trả phí)
        DateTime hetHan;
        if (!info.MienPhi)
        {
            var goiHienTai = await _db.DangKyApps
                .Where(d => d.MaThietBi == maThietBi && d.NgayHetHan > now)
                .OrderByDescending(d => d.NgayHetHan)
                .FirstOrDefaultAsync();
            hetHan = (goiHienTai?.NgayHetHan ?? now).AddDays(info.SoNgay);
        }
        else
        {
            hetHan = now.AddDays(info.SoNgay);
        }

        _db.DangKyApps.Add(new DangKyApp
        {
            Id         = Guid.NewGuid(),
            MaThietBi  = maThietBi,
            LoaiGoi    = req.LoaiGoi,
            NgayBatDau = now,
            NgayHetHan = hetHan,
            SoTien     = info.Gia
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message      = $"Đăng ký {info.Ten} thành công!",
            NgayHetHan   = hetHan,
            SoNgayConLai = info.SoNgay,
            SoTien       = info.Gia,
            MienPhi      = info.MienPhi
        });
    }

    // -----------------------------------------------------------------------
    // POST /api/subscription/request
    // Tạo yêu cầu thanh toán qua QR — gọi sau khi khách đã chuyển khoản.
    // Body: { MaThietBi, LoaiGoi }
    // -----------------------------------------------------------------------
    [HttpPost("request")]
    public async Task<IActionResult> CreateRequest([FromBody] PurchaseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.MaThietBi))
            return BadRequest(new { message = "MaThietBi không được trống." });

        if (!Goi.TryGetValue(req.LoaiGoi, out var info) || info.MienPhi)
            return BadRequest(new { message = "Loại gói không hợp lệ. Chọn: ngay, tuan, thang, nam." });

        // Tạo mã nội dung chuyển khoản dễ nhận diện
        var maThietBi = LichSuPhatInputNormalizer.NormalizeMaThietBi(req.MaThietBi);
        if (string.IsNullOrWhiteSpace(maThietBi))
            return BadRequest(new { message = "MaThietBi không hợp lệ." });

        var shortId = maThietBi[..Math.Min(6, maThietBi.Length)].ToUpper();
        var noiDung = $"VKT {req.LoaiGoi.ToUpper()} {shortId}";

        var yc = new VinhKhanhTour.API.Models.YeuCauThanhToan
        {
            Id            = Guid.NewGuid(),
            MaThietBi     = maThietBi,
            LoaiGoi       = req.LoaiGoi,
            SoTien        = info.Gia,
            NoiDungChuyen = noiDung,
            TrangThai     = "cho_duyet",
            NgayTao       = DateTime.UtcNow
        };

        _db.YeuCauThanhToans.Add(yc);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            YeuCauId      = yc.Id,
            SoTien        = info.Gia,
            Ten           = info.Ten,
            NoiDungChuyen = noiDung,
            TrangThai     = yc.TrangThai
        });
    }

    // -----------------------------------------------------------------------
    // GET /api/subscription/request/{yeuCauId}
    // Kiểm tra trạng thái yêu cầu — app polling mỗi 10 giây.
    // -----------------------------------------------------------------------
    [HttpGet("request/{yeuCauId:guid}")]
    public async Task<IActionResult> GetRequestStatus(Guid yeuCauId)
    {
        var yc = await _db.YeuCauThanhToans.AsNoTracking()
            .FirstOrDefaultAsync(y => y.Id == yeuCauId);

        if (yc == null) return NotFound(new { message = "Yêu cầu không tồn tại." });

        // Tính ngày hết hạn nếu đã duyệt
        DateTime? ngayHetHan = null;
        if (yc.TrangThai == "da_duyet")
        {
            var existing = await _db.DangKyApps.AsNoTracking()
                .Where(d => d.MaThietBi == yc.MaThietBi && d.NgayHetHan > DateTime.UtcNow)
                .OrderByDescending(d => d.NgayHetHan)
                .FirstOrDefaultAsync();
            ngayHetHan = existing?.NgayHetHan;
        }

        return Ok(new
        {
            yc.Id,
            yc.MaThietBi,
            yc.LoaiGoi,
            yc.SoTien,
            yc.TrangThai,
            yc.GhiChuAdmin,
            yc.NgayTao,
            yc.NgayDuyet,
            NgayHetHan = ngayHetHan
        });
    }

    // -----------------------------------------------------------------------
    // POST /api/subscription/approve/{yeuCauId}
    // Admin duyệt yêu cầu → kích hoạt gói → cập nhật trạng thái.
    // Body: { GhiChu? }
    // -----------------------------------------------------------------------
    [HttpPost("approve/{yeuCauId:guid}")]
    public async Task<IActionResult> Approve(Guid yeuCauId, [FromBody] ApproveRequest req)
    {
        var yc = await _db.YeuCauThanhToans.FirstOrDefaultAsync(y => y.Id == yeuCauId);
        if (yc == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (yc.TrangThai != "cho_duyet")
            return BadRequest(new { message = $"Yêu cầu đã ở trạng thái '{yc.TrangThai}'." });

        if (!Goi.TryGetValue(yc.LoaiGoi, out var info))
            return BadRequest(new { message = "Loại gói không hợp lệ." });

        var now = DateTime.UtcNow;

        // Gia hạn nối tiếp nếu thiết bị đang có gói chưa hết
        var goiHienTai = await _db.DangKyApps
            .Where(d => d.MaThietBi == yc.MaThietBi && d.NgayHetHan > now)
            .OrderByDescending(d => d.NgayHetHan)
            .FirstOrDefaultAsync();

        var mocBatDau = goiHienTai?.NgayHetHan ?? now;
        var hetHan    = mocBatDau.AddDays(info.SoNgay);

        _db.DangKyApps.Add(new VinhKhanhTour.API.Models.DangKyApp
        {
            Id         = Guid.NewGuid(),
            MaThietBi  = yc.MaThietBi,
            LoaiGoi    = yc.LoaiGoi,
            NgayBatDau = now,
            NgayHetHan = hetHan,
            SoTien     = yc.SoTien
        });

        yc.TrangThai  = "da_duyet";
        yc.NgayDuyet  = now;
        yc.GhiChuAdmin = req.GhiChu;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message    = $"Đã duyệt và kích hoạt gói {info.Ten} cho thiết bị.",
            NgayHetHan = hetHan
        });
    }

    // -----------------------------------------------------------------------
    // POST /api/subscription/reject/{yeuCauId}
    // Admin từ chối yêu cầu.
    // Body: { GhiChu }
    // -----------------------------------------------------------------------
    [HttpPost("reject/{yeuCauId:guid}")]
    public async Task<IActionResult> Reject(Guid yeuCauId, [FromBody] ApproveRequest req)
    {
        var yc = await _db.YeuCauThanhToans.FirstOrDefaultAsync(y => y.Id == yeuCauId);
        if (yc == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (yc.TrangThai != "cho_duyet")
            return BadRequest(new { message = $"Yêu cầu đã ở trạng thái '{yc.TrangThai}'." });

        yc.TrangThai   = "tu_choi";
        yc.NgayDuyet   = DateTime.UtcNow;
        yc.GhiChuAdmin = req.GhiChu;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã từ chối yêu cầu." });
    }

    // -----------------------------------------------------------------------
    // GET /api/subscription/requests
    // Danh sách tất cả yêu cầu (dùng cho CMS). Query: ?trangthai=cho_duyet
    // -----------------------------------------------------------------------
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] string? trangthai = null)
    {
        var query = _db.YeuCauThanhToans.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(trangthai))
            query = query.Where(y => y.TrangThai == trangthai);

        var list = await query
            .OrderByDescending(y => y.NgayTao)
            .Select(y => new
            {
                y.Id, y.MaThietBi, y.LoaiGoi, y.SoTien,
                y.NoiDungChuyen, y.TrangThai, y.GhiChuAdmin,
                y.NgayTao, y.NgayDuyet
            })
            .ToListAsync();

        return Ok(list);
    }
}

public class PurchaseRequest
{
    public string MaThietBi { get; set; } = "";
    public string LoaiGoi   { get; set; } = "thang";
}

public class ApproveRequest
{
    public string? GhiChu { get; set; }
}
