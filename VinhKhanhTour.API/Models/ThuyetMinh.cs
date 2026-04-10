using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

[Table("thuyetminh")]
public class ThuyetMinh 
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("poiid")]
    public Guid POIId { get; set; }

    [Column("thutu")]
    public int ThuTu { get; set; } = 1;

    [Column("trangthai")]
    public bool TrangThai { get; set; } = true;

    public POI POI { get; set; } = null!;
    public ICollection<BanDich> BanDichs { get; set; } = [];
}