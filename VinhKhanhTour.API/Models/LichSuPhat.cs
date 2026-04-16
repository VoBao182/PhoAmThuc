using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

[Table("lichsuphat")]
public class LichSuPhat
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("taikhoanid")]
    public Guid? TaiKhoanId { get; set; }

    [Column("poiid")]
    public Guid? POIId { get; set; }

    [Column("thuyetminhid")]
    public Guid? ThuyetMinhId { get; set; }

    [Column("thoigian")]
    public DateTime ThoiGian { get; set; } = DateTime.UtcNow;

    [Column("ngonngudung")]
    [MaxLength(20)]
    public string? NgonNguDung { get; set; }

    [Column("nguon")]
    [MaxLength(50)]
    public string? Nguon { get; set; }

    // Database hien tai chua co cot nay; tam bo qua mapping de tranh loi SaveChanges.
    [NotMapped]
    public int? ThoiLuongNghe { get; set; }

    /// <summary>Mã thiết bị ẩn danh — dùng khi khách không đăng nhập</summary>
    [Column("mathietbi")]
    public string? MaThietBi { get; set; }

    public POI? POI { get; set; }
}
