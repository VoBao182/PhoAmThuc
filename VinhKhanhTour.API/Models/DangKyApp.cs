using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

/// <summary>
/// Gói đăng ký sử dụng app — khách hàng ẩn danh, định danh bằng mã thiết bị.
/// LoaiGoi: "ngay" | "thang" | "nam"
/// </summary>
[Table("dangkyapp")]
public class DangKyApp
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Mã thiết bị (UUID sinh lần đầu, lưu trong Preferences app)</summary>
    [Column("mathietbi")]
    public string MaThietBi { get; set; } = "";

    /// <summary>"ngay" | "thang" | "nam"</summary>
    [Column("loaigoi")]
    public string LoaiGoi { get; set; } = "thang";

    [Column("ngaybatdau")]
    public DateTime NgayBatDau { get; set; } = DateTime.UtcNow;

    [Column("ngayhethan")]
    public DateTime NgayHetHan { get; set; }

    [Column("sotien")]
    public decimal SoTien { get; set; }
}
