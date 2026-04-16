using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.ThanhToan;

public class PoiThanhToanViewModel
{
    public Guid     POIId            { get; set; }
    public string   TenPOI           { get; set; } = "";
    public string?  DiaChi           { get; set; }
    public DateTime? NgayHetHanDuyTri { get; set; }
    public decimal  PhiDuyTriThang   { get; set; } = 50_000;
}

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<PoiThanhToanViewModel> DanhSachPOI  { get; set; } = [];
    public int    SoQuanDaDong   { get; set; }
    public int    SoQuanQuaHan   { get; set; }
    public int    SoQuanSapHan   { get; set; }
    public decimal TongThuThang  { get; set; }
    public string? ThongBao      { get; set; }

    public async Task OnGetAsync(string? msg)
    {
        ThongBao = msg;
        var now = DateTime.UtcNow;

        // Lấy tất cả POI kèm gói dịch vụ (left join)
        var pois = await _db.POIs.AsNoTracking()
            .OrderBy(p => p.NgayHetHanDuyTri)
            .ToListAsync();

        var goiList = await _db.DangKyDichVus.AsNoTracking()
            .Where(d => d.TrangThai)
            .ToListAsync();

        DanhSachPOI = pois.Select(p =>
        {
            var goi = goiList.FirstOrDefault(g => g.POIId == p.Id);
            return new PoiThanhToanViewModel
            {
                POIId            = p.Id,
                TenPOI           = p.TenPOI,
                DiaChi           = p.DiaChi,
                NgayHetHanDuyTri = p.NgayHetHanDuyTri,
                PhiDuyTriThang   = goi?.PhiDuyTriThang ?? 50_000m
            };
        }).ToList();

        SoQuanQuaHan = DanhSachPOI.Count(p => p.NgayHetHanDuyTri == null || p.NgayHetHanDuyTri < now);
        SoQuanSapHan = DanhSachPOI.Count(p =>
            p.NgayHetHanDuyTri.HasValue &&
            p.NgayHetHanDuyTri >= now &&
            (p.NgayHetHanDuyTri.Value - now).TotalDays <= 7);
        SoQuanDaDong = DanhSachPOI.Count(p =>
            p.NgayHetHanDuyTri.HasValue && p.NgayHetHanDuyTri >= now);

        // Tổng thu tháng hiện tại
        var kyThang = now.ToString("yyyy-MM");
        TongThuThang = await _db.HoaDons.AsNoTracking()
            .Where(h => h.LoaiPhi == "duytri" && h.KyThanhToan == kyThang)
            .SumAsync(h => h.SoTien);
    }
}
