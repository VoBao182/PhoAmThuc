using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly AppDbContext _db;
    public PoiController(AppDbContext db) => _db = db;

    // GET /api/poi — lấy POI đang hoạt động
    // Chỉ hiện quán có NgayHetHanDuyTri còn hạn (null hoặc quá hạn → ẩn)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var now = DateTime.UtcNow;
        var pois = await _db.POIs
            .Where(p => p.TrangThai
                     && p.NgayHetHanDuyTri.HasValue
                     && p.NgayHetHanDuyTri > now)
            .OrderBy(p => p.MucUuTien)
            .Select(p => new {
                p.Id,
                p.TenPOI,
                p.KinhDo,
                p.ViDo,
                p.BanKinh,
                p.MucUuTien,
                p.AnhDaiDien,
                p.SDT,
                p.DiaChi
            })
            .ToListAsync();
        return Ok(pois);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string lang = "vi")
    {
        var poi = await _db.POIs
            .Include(p => p.MonAns.Where(m => m.TinhTrang))
            .Include(p => p.ThuyetMinhs.Where(t => t.TrangThai))
                .ThenInclude(t => t.BanDichs)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poi == null) return NotFound();

        // Lấy nội dung thuyết minh theo ngôn ngữ
        var tm = poi.ThuyetMinhs.FirstOrDefault();
        var banDich = tm?.BanDichs
            .FirstOrDefault(b => b.NgonNgu == lang)
            ?? tm?.BanDichs.FirstOrDefault(b => b.NgonNgu == "vi");

        return Ok(new
        {
            poi.Id,
            poi.TenPOI,
            poi.KinhDo,
            poi.ViDo,
            poi.DiaChi,
            poi.SDT,
            poi.AnhDaiDien,
            NoiDungThuyetMinh = banDich?.NoiDung ?? "",
            FileAudio = banDich?.FileAudio,
            MonAns = poi.MonAns.Select(m => new {
                m.Id,
                m.TenMonAn,
                m.DonGia,
                m.PhanLoai,
                m.MoTa,
                m.HinhAnh
            })
        });
    }
}
