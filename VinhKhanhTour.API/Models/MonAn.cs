using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

[Table("monan")]
public class MonAn
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("poiid")]
    public Guid POIId { get; set; }

    [Column("tenmonan")]
    public string TenMonAn { get; set; } = "";

    [Column("dongia")]
    public decimal DonGia { get; set; }

    [Column("phanloai")]
    public string? PhanLoai { get; set; }

    [Column("mota")]
    public string? MoTa { get; set; }

    [Column("hinhanh")]
    public string? HinhAnh { get; set; }

    [Column("tinhtrang")]
    public bool TinhTrang { get; set; } = true;

    public POI POI { get; set; } = null!;
}