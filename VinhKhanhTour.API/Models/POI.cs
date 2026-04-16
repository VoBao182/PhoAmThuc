using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

[Table("poi")]
public class POI
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenpoi")]
    public string TenPOI { get; set; } = "";

    [Column("kinhdo")]
    public double KinhDo { get; set; }

    [Column("vido")]
    public double ViDo { get; set; }

    [Column("bankinh")]
    public int BanKinh { get; set; } = 30;

    [Column("mucuutien")]
    public int MucUuTien { get; set; } = 1;

    [Column("trangthai")]
    public bool TrangThai { get; set; } = true;

    [Column("sdt")]
    public string? SDT { get; set; }

    [Column("diachi")]
    public string? DiaChi { get; set; }

    [Column("anhdaidien")]
    public string? AnhDaiDien { get; set; }

    /// <summary>
    /// Hạn duy trì hệ thống của quán.
    /// Null = chưa đăng ký; quá hạn = POI không hiển thị trên app.
    /// </summary>
    [Column("ngayhethanduytri")]
    public DateTime? NgayHetHanDuyTri { get; set; }

    public ICollection<ThuyetMinh> ThuyetMinhs { get; set; } = [];
    public ICollection<MonAn> MonAns { get; set; } = [];
    public ICollection<HoaDon> HoaDons { get; set; } = [];
}