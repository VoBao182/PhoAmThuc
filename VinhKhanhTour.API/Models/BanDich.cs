using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VinhKhanhTour.API.Models;

[Table("bandich")]
public class BanDich
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("thuyetminhid")]
    public Guid ThuyetMinhId { get; set; }

    [Column("ngonngu")]
    public string NgonNgu { get; set; } = "vi";

    [Column("noidung")]
    public string NoiDung { get; set; } = "";

    [Column("fileaudio")]
    public string? FileAudio { get; set; }

    public ThuyetMinh ThuyetMinh { get; set; } = null!;
}