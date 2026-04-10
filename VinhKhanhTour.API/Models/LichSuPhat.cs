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

    [Column("ngonnguDung")]
    public string? NgonNguDung { get; set; }

    [Column("nguon")]
    public string? Nguon { get; set; }

    [Column("thoiluongnge")]
    public int? ThoiLuongNghe { get; set; }

    public POI? POI { get; set; }
}