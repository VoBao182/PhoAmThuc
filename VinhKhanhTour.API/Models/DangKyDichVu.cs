using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

/// <summary>
/// Gói dịch vụ đăng ký cho mỗi POI (quán).
/// Lưu mức phí và thời hạn duy trì hiện tại.
/// </summary>
[Table("dangkydichvu")]
public class DangKyDichVu
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("poiid")]
    public Guid POIId { get; set; }

    /// <summary>Phí duy trì hàng tháng (VND)</summary>
    [Column("phiduytrithang")]
    public decimal PhiDuyTriThang { get; set; } = 50_000;

    /// <summary>Phí mỗi lần convert TTS (VND)</summary>
    [Column("phiconvert")]
    public decimal PhiConvert { get; set; } = 20_000;

    [Column("ngaybatdau")]
    public DateTime NgayBatDau { get; set; } = DateTime.UtcNow;

    /// <summary>Hạn duy trì; nếu quá hạn POI bị tắt tự động</summary>
    [Column("ngayhethan")]
    public DateTime NgayHetHan { get; set; }

    /// <summary>true = đang hoạt động</summary>
    [Column("trangthai")]
    public bool TrangThai { get; set; } = true;

    public POI? POI { get; set; }
}
