using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.ThanhToan;

public class PoiThanhToanViewModel
{
    public Guid POIId { get; set; }
    public string TenPOI { get; set; } = "";
    public string? DiaChi { get; set; }
    public DateTime? NgayHetHanDuyTri { get; set; }
    public decimal PhiDuyTriThang { get; set; } = 50_000;
}

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<PoiThanhToanViewModel> DanhSachPOI { get; set; } = [];
    public int SoQuanDaDong { get; set; }
    public int SoQuanQuaHan { get; set; }
    public int SoQuanSapHan { get; set; }
    public decimal TongThuThang { get; set; }
    public string? ThongBao { get; set; }
    public string? LoiMsg { get; set; }

    public async Task OnGetAsync(string? msg)
    {
        ThongBao = msg;
        var now = DateTime.UtcNow;

        try
        {
            var pois = await _db.POIs
                .AsNoTracking()
                .OrderBy(p => p.NgayHetHanDuyTri)
                .Select(p => new PoiThanhToanViewModel
                {
                    POIId = p.Id,
                    TenPOI = p.TenPOI,
                    DiaChi = p.DiaChi,
                    NgayHetHanDuyTri = p.NgayHetHanDuyTri,
                    PhiDuyTriThang = 50_000m
                })
                .ToListAsync();

            var activeFees = await _db.DangKyDichVus
                .AsNoTracking()
                .Where(d => d.TrangThai)
                .Select(d => new
                {
                    d.POIId,
                    d.PhiDuyTriThang,
                    d.NgayBatDau
                })
                .ToListAsync();

            var latestFeeByPoi = activeFees
                .GroupBy(d => d.POIId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(d => d.NgayBatDau).First().PhiDuyTriThang);

            foreach (var poi in pois)
            {
                if (latestFeeByPoi.TryGetValue(poi.POIId, out var fee))
                    poi.PhiDuyTriThang = fee;
            }

            DanhSachPOI = pois;
            SoQuanQuaHan = DanhSachPOI.Count(p => p.NgayHetHanDuyTri == null || p.NgayHetHanDuyTri < now);
            SoQuanSapHan = DanhSachPOI.Count(p =>
                p.NgayHetHanDuyTri.HasValue &&
                p.NgayHetHanDuyTri >= now &&
                (p.NgayHetHanDuyTri.Value - now).TotalDays <= 7);
            SoQuanDaDong = DanhSachPOI.Count(p =>
                p.NgayHetHanDuyTri.HasValue && p.NgayHetHanDuyTri >= now);

            var kyThang = now.ToString("yyyy-MM");
            TongThuThang = await _db.HoaDons.AsNoTracking()
                .Where(h => h.LoaiPhi == "duytri" && h.KyThanhToan == kyThang)
                .Select(h => (decimal?)h.SoTien)
                .SumAsync() ?? 0m;
        }
        catch (Exception ex)
        {
            DanhSachPOI = [];
            SoQuanDaDong = 0;
            SoQuanQuaHan = 0;
            SoQuanSapHan = 0;
            TongThuThang = 0m;
            LoiMsg = $"Không thể tải trang phí duy trì: {ex.GetBaseException().Message}";
        }
    }
}
