using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.API.Controllers;

/// <summary>
/// Xử lý phí duy trì (ghi nhận bởi admin trên CMS) và phí convert TTS (thanh toán trên app).
///
/// Luồng phí duy trì:
///   Admin đăng nhập CMS → chọn POI → ghi nhận đã thu tiền tháng → POST /api/payment/maintenance
///   → Hệ thống tạo HoaDon + gia hạn NgayHetHanDuyTri của POI thêm 1 tháng.
///
/// Luồng phí convert:
///   Chủ quán dùng app → nhấn "Convert TTS" → POST /api/payment/convert/{poiId}
///   → Hệ thống tạo HoaDon convert, sau đó CMS trigger job chạy TTS.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _db;
    public PaymentController(AppDbContext db) => _db = db;

    // -----------------------------------------------------------------------
    // GET /api/payment/status/{poiId}
    // Trả về trạng thái thanh toán của POI (dùng cho CMS và app)
    // -----------------------------------------------------------------------
    [HttpGet("status/{poiId:guid}")]
    public async Task<IActionResult> GetStatus(Guid poiId)
    {
        var poi = await _db.POIs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == poiId);

        if (poi == null) return NotFound(new { message = "POI không tồn tại." });

        var goi = await _db.DangKyDichVus.AsNoTracking()
            .FirstOrDefaultAsync(d => d.POIId == poiId && d.TrangThai);

        var now = DateTime.UtcNow;
        bool hetHanDuyTri = poi.NgayHetHanDuyTri == null || poi.NgayHetHanDuyTri < now;
        int soNgayConLai   = poi.NgayHetHanDuyTri.HasValue
            ? (int)(poi.NgayHetHanDuyTri.Value - now).TotalDays
            : 0;

        return Ok(new
        {
            PoiId           = poiId,
            TenPOI          = poi.TenPOI,
            NgayHetHanDuyTri = poi.NgayHetHanDuyTri,
            HetHanDuyTri    = hetHanDuyTri,
            SoNgayConLai    = soNgayConLai,
            PhiDuyTriThang  = goi?.PhiDuyTriThang ?? 50_000m,
            PhiConvert      = goi?.PhiConvert      ?? 20_000m
        });
    }

    // -----------------------------------------------------------------------
    // GET /api/payment/history/{poiId}
    // Lịch sử hóa đơn của một POI
    // -----------------------------------------------------------------------
    [HttpGet("history/{poiId:guid}")]
    public async Task<IActionResult> GetHistory(Guid poiId)
    {
        var hds = await _db.HoaDons
            .AsNoTracking()
            .Where(h => h.POIId == poiId)
            .OrderByDescending(h => h.NgayThanhToan)
            .Select(h => new
            {
                h.Id,
                h.LoaiPhi,
                h.SoTien,
                h.NgayThanhToan,
                h.KyThanhToan,
                h.GhiChu
            })
            .ToListAsync();

        return Ok(hds);
    }

    // -----------------------------------------------------------------------
    // POST /api/payment/maintenance
    // Ghi nhận thu phí duy trì (gọi từ CMS bởi admin/quanly)
    // Body: { PoiId, TaiKhoanId, SoThangGiaHan, GhiChu? }
    // -----------------------------------------------------------------------
    [HttpPost("maintenance")]
    public async Task<IActionResult> RecordMaintenance([FromBody] MaintenanceRequest req)
    {
        if (req.SoThangGiaHan <= 0)
            return BadRequest(new { message = "Số tháng gia hạn phải lớn hơn 0." });

        var poi = await _db.POIs.FirstOrDefaultAsync(p => p.Id == req.PoiId);
        if (poi == null) return NotFound(new { message = "POI không tồn tại." });

        // Lấy gói dịch vụ để biết đơn giá
        var goi = await _db.DangKyDichVus
            .FirstOrDefaultAsync(d => d.POIId == req.PoiId && d.TrangThai);

        decimal donGia = goi?.PhiDuyTriThang ?? 50_000m;
        decimal tongTien = donGia * req.SoThangGiaHan;

        var now = DateTime.UtcNow;

        // Gia hạn: tính từ ngày hiện tại hoặc từ hạn cũ (nếu chưa hết)
        DateTime mocGiaHan = (poi.NgayHetHanDuyTri.HasValue && poi.NgayHetHanDuyTri > now)
            ? poi.NgayHetHanDuyTri.Value
            : now;
        poi.NgayHetHanDuyTri = mocGiaHan.AddMonths(req.SoThangGiaHan);
        poi.TrangThai = true;   // kích hoạt lại nếu đang bị tắt do quá hạn

        // Tạo hóa đơn cho từng tháng (để dễ tra cứu kỳ nào đã đóng)
        for (int i = 0; i < req.SoThangGiaHan; i++)
        {
            var kyThanhToan = mocGiaHan.AddMonths(i).ToString("yyyy-MM");
            var hd = new HoaDon
            {
                Id             = Guid.NewGuid(),
                POIId          = req.PoiId,
                TaiKhoanId     = req.TaiKhoanId,
                LoaiPhi        = "duytri",
                SoTien         = donGia,
                NgayThanhToan  = now,
                KyThanhToan    = kyThanhToan,
                GhiChu         = req.GhiChu
            };
            _db.HoaDons.Add(hd);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message          = $"Đã gia hạn {req.SoThangGiaHan} tháng. Tổng: {tongTien:N0}đ",
            NgayHetHanMoi    = poi.NgayHetHanDuyTri,
            TongTien         = tongTien
        });
    }

    // -----------------------------------------------------------------------
    // POST /api/payment/convert/{poiId}
    // Thanh toán phí convert TTS — gọi từ app bởi chủ quán
    // Body: { TaiKhoanId, GhiChu? }
    // -----------------------------------------------------------------------
    [HttpPost("convert/{poiId:guid}")]
    public async Task<IActionResult> PayConvert(Guid poiId, [FromBody] ConvertRequest req)
    {
        var poi = await _db.POIs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == poiId);
        if (poi == null) return NotFound(new { message = "POI không tồn tại." });

        // Kiểm tra phí duy trì còn hạn — quán hết hạn duy trì không được convert
        if (poi.NgayHetHanDuyTri == null || poi.NgayHetHanDuyTri < DateTime.UtcNow)
            return BadRequest(new { message = "POI đã hết hạn duy trì. Vui lòng gia hạn trước khi convert." });

        var goi = await _db.DangKyDichVus.AsNoTracking()
            .FirstOrDefaultAsync(d => d.POIId == poiId && d.TrangThai);

        decimal phiConvert = goi?.PhiConvert ?? 20_000m;

        var hd = new HoaDon
        {
            Id            = Guid.NewGuid(),
            POIId         = poiId,
            TaiKhoanId    = req.TaiKhoanId,
            LoaiPhi       = "convert",
            SoTien        = phiConvert,
            NgayThanhToan = DateTime.UtcNow,
            KyThanhToan   = null,   // không có kỳ, chỉ tính theo lần
            GhiChu        = req.GhiChu ?? "Thanh toán phí convert TTS từ app"
        };

        _db.HoaDons.Add(hd);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message    = "Thanh toán phí convert thành công.",
            HoaDonId   = hd.Id,
            SoTien     = phiConvert,
            // Flag để app biết cần trigger job convert TTS
            CanConvert = true
        });
    }

    // -----------------------------------------------------------------------
    // GET /api/payment/overdue
    // Danh sách POI quá hạn duy trì (dùng cho dashboard CMS)
    // -----------------------------------------------------------------------
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue()
    {
        var now = DateTime.UtcNow;
        var dsQuaHan = await _db.POIs
            .AsNoTracking()
            .Where(p => p.NgayHetHanDuyTri == null || p.NgayHetHanDuyTri < now)
            .OrderBy(p => p.NgayHetHanDuyTri)
            .Select(p => new
            {
                p.Id,
                p.TenPOI,
                p.DiaChi,
                p.NgayHetHanDuyTri,
                SoNgayQuaHan = p.NgayHetHanDuyTri.HasValue
                    ? (int)(now - p.NgayHetHanDuyTri.Value).TotalDays
                    : -1    // -1 = chưa bao giờ đóng tiền
            })
            .ToListAsync();

        return Ok(dsQuaHan);
    }
}

public class MaintenanceRequest
{
    public Guid   PoiId          { get; set; }
    public Guid?  TaiKhoanId     { get; set; }
    public int    SoThangGiaHan  { get; set; } = 1;
    public string? GhiChu        { get; set; }
}

public class ConvertRequest
{
    public Guid?  TaiKhoanId { get; set; }
    public string? GhiChu    { get; set; }
}
