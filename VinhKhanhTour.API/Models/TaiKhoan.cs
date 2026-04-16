using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

[Table("taikhoan")]
public class TaiKhoan
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tendangnhap")]
    public string TenDangNhap { get; set; } = "";

    [Column("matkhau")]
    public string MatKhau { get; set; } = "";   // BCrypt hash

    [Column("tentaikhoan")]
    public string? TenTaiKhoan { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("sodienthoai")]
    public string? SoDienThoai { get; set; }

    /// <summary>khach | admin | quanly</summary>
    [Column("vaitro")]
    public string VaiTro { get; set; } = "khach";

    [Column("trangthai")]
    public bool TrangThai { get; set; } = true;

    [Column("ngaytao")]
    public DateTime NgayTao { get; set; } = DateTime.UtcNow;

    /// <summary>POI mà tài khoản quản lý (chỉ dùng cho VaiTro = "quanly")</summary>
    [Column("poiid")]
    public Guid? PoiId { get; set; }
}
