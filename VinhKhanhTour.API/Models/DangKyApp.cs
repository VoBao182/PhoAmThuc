using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

/// <summary>
/// G�i dang k� s? d?ng app - kh�ch h�ng ?n danh, d?nh danh b?ng m� thi?t b?.
/// LoaiGoi: "ngay" | "thang" | "nam"
/// </summary>
[Table("dangkyapp")]
public class DangKyApp
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>M� thi?t b? (UUID sinh l?n d?u, luu trong Preferences app)</summary>
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
