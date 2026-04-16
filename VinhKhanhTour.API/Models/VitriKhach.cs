using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

/// <summary>
/// Vị trí thực tế của khách (heartbeat từ app).
/// Mỗi thiết bị chỉ có 1 dòng — được upsert mỗi lần heartbeat.
/// </summary>
[Table("vitrikhach")]
public class VitriKhach
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("mathietbi")]
    public string MaThietBi { get; set; } = "";

    [Column("lat")]
    public double Lat { get; set; }

    [Column("lng")]
    public double Lng { get; set; }

    /// <summary>Thời điểm heartbeat cuối cùng. "Online" nếu < 2 phút trước.</summary>
    [Column("lancuoi_heartbeat")]
    public DateTime LanCuoiHeartbeat { get; set; } = DateTime.UtcNow;

    /// <summary>ID của POI khách đang đứng gần nhất (null nếu không trong vùng POI nào)</summary>
    [Column("poiid_hientai")]
    public Guid? PoiIdHienTai { get; set; }

    /// <summary>Tên POI hiện tại (lưu sẵn để không cần JOIN)</summary>
    [Column("ten_poi_hientai")]
    public string? TenPoiHienTai { get; set; }
}
