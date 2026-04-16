using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

/// <summary>
/// Hóa đơn thanh toán.
/// LoaiPhi = "duytri"  → phí duy trì hàng tháng (ghi nhận qua CMS bởi quản lý)
/// LoaiPhi = "convert" → phí convert TTS (thanh toán trực tiếp trên app)
/// </summary>
[Table("hoadon")]
public class HoaDon
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("poiid")]
    public Guid POIId { get; set; }

    /// <summary>Tài khoản người thực hiện giao dịch (admin ghi nhận hoặc chủ quán)</summary>
    [Column("taikhoanid")]
    public Guid? TaiKhoanId { get; set; }

    /// <summary>"duytri" | "convert"</summary>
    [Column("loaiphi")]
    public string LoaiPhi { get; set; } = "duytri";

    [Column("sotien")]
    public decimal SoTien { get; set; }

    [Column("ngaythanhtoan")]
    public DateTime NgayThanhToan { get; set; } = DateTime.UtcNow;

    /// <summary>Kỳ thanh toán (ví dụ "2025-04" cho tháng 4/2025). Null nếu là phí convert.</summary>
    [Column("kythanhtoan")]
    public string? KyThanhToan { get; set; }

    [Column("ghichu")]
    public string? GhiChu { get; set; }

    public POI? POI { get; set; }
}
