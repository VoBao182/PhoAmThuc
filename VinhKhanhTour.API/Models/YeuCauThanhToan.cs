using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

/// <summary>
/// Yêu cầu thanh toán gói app gửi từ thiết bị.
/// Khách chuyển khoản QR → tạo bản ghi này → admin duyệt → kích hoạt gói.
/// TrangThai: "cho_duyet" | "da_duyet" | "tu_choi"
/// </summary>
[Table("yeucauthanhtoan")]
public class YeuCauThanhToan
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("mathietbi")]
    public string MaThietBi { get; set; } = "";

    /// <summary>"ngay" | "thang" | "nam"</summary>
    [Column("loaigoi")]
    public string LoaiGoi { get; set; } = "thang";

    [Column("sotien")]
    public decimal SoTien { get; set; }

    /// <summary>Nội dung chuyển khoản để admin khớp với sao kê ngân hàng</summary>
    [Column("noidung_chuyen")]
    public string NoiDungChuyen { get; set; } = "";

    /// <summary>"cho_duyet" | "da_duyet" | "tu_choi"</summary>
    [Column("trangthai")]
    public string TrangThai { get; set; } = "cho_duyet";

    [Column("ghichu_admin")]
    public string? GhiChuAdmin { get; set; }

    [Column("ngaytao")]
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;

    [Column("ngayduyet")]
    public DateTime? NgayDuyet { get; set; }
}
