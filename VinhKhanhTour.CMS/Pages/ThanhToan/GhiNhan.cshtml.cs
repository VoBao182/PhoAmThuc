using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.ThanhToan;

public class LichSuItem
{
    public string?  KyThanhToan   { get; set; }
    public DateTime NgayThanhToan { get; set; }
    public decimal  SoTien        { get; set; }
}

public class GhiNhanModel : PageModel
{
    private readonly AppDbContext _db;
    public GhiNhanModel(AppDbContext db) => _db = db;

    public Guid     PoiId            { get; set; }
    public string   TenPOI           { get; set; } = "";
    public string?  DiaChi           { get; set; }
    public DateTime? NgayHetHanDuyTri { get; set; }
    public decimal  PhiDuyTriThang   { get; set; } = 50_000m;
    public string?  LỗiMessage       { get; set; }
    public List<LichSuItem> LichSuGanDay { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid poiId)
    {
        await LoadPoiAsync(poiId);
        if (TenPOI == "") return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        Guid    poiId,
        int     soThangGiaHan,
        decimal phiDuyTriThang,
        string? ghiChu)
    {
        if (soThangGiaHan <= 0)
        {
            await LoadPoiAsync(poiId);
            LỗiMessage = "Số tháng gia hạn phải lớn hơn 0.";
            return Page();
        }

        var poi = await _db.POIs.FirstOrDefaultAsync(p => p.Id == poiId);
        if (poi == null) return NotFound();

        var now = DateTime.UtcNow;
        DateTime mocGiaHan = (poi.NgayHetHanDuyTri.HasValue && poi.NgayHetHanDuyTri > now)
            ? poi.NgayHetHanDuyTri.Value
            : now;

        poi.NgayHetHanDuyTri = mocGiaHan.AddMonths(soThangGiaHan);
        poi.TrangThai = true;

        // Cập nhật đơn giá gói dịch vụ nếu khác
        var goi = await _db.DangKyDichVus.FirstOrDefaultAsync(d => d.POIId == poiId && d.TrangThai);
        if (goi == null)
        {
            goi = new DangKyDichVu
            {
                Id              = Guid.NewGuid(),
                POIId           = poiId,
                PhiDuyTriThang  = phiDuyTriThang,
                PhiConvert      = 20_000m,
                NgayBatDau      = now,
                NgayHetHan      = poi.NgayHetHanDuyTri.Value,
                TrangThai       = true
            };
            _db.DangKyDichVus.Add(goi);
        }
        else
        {
            goi.PhiDuyTriThang = phiDuyTriThang;
            goi.NgayHetHan     = poi.NgayHetHanDuyTri.Value;
        }

        for (int i = 0; i < soThangGiaHan; i++)
        {
            _db.HoaDons.Add(new HoaDon
            {
                Id            = Guid.NewGuid(),
                POIId         = poiId,
                TaiKhoanId    = null,
                LoaiPhi       = "duytri",
                SoTien        = phiDuyTriThang,
                NgayThanhToan = now,
                KyThanhToan   = mocGiaHan.AddMonths(i).ToString("yyyy-MM"),
                GhiChu        = ghiChu
            });
        }

        await _db.SaveChangesAsync();

        decimal tongTien = phiDuyTriThang * soThangGiaHan;
        return RedirectToPage("/ThanhToan/Index",
            new { msg = $"Đã gia hạn {poi.TenPOI} thêm {soThangGiaHan} tháng. Tổng: {tongTien:N0}đ" });
    }

    private async Task LoadPoiAsync(Guid poiId)
    {
        PoiId = poiId;
        var poi = await _db.POIs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == poiId);
        if (poi == null) return;

        TenPOI            = poi.TenPOI;
        DiaChi            = poi.DiaChi;
        NgayHetHanDuyTri  = poi.NgayHetHanDuyTri;

        var goi = await _db.DangKyDichVus.AsNoTracking()
            .FirstOrDefaultAsync(d => d.POIId == poiId && d.TrangThai);
        PhiDuyTriThang = goi?.PhiDuyTriThang ?? 50_000m;

        LichSuGanDay = await _db.HoaDons.AsNoTracking()
            .Where(h => h.POIId == poiId && h.LoaiPhi == "duytri")
            .OrderByDescending(h => h.NgayThanhToan)
            .Take(5)
            .Select(h => new LichSuItem
            {
                KyThanhToan   = h.KyThanhToan,
                NgayThanhToan = h.NgayThanhToan,
                SoTien        = h.SoTien
            })
            .ToListAsync();
    }
}
