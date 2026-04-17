using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.DuyetThanhToan;

public class YeuCauViewModel
{
    public Guid Id { get; set; }
    public string MaThietBi { get; set; } = "";
    public string LoaiGoi { get; set; } = "";
    public decimal SoTien { get; set; }
    public string NoiDungChuyen { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public string? GhiChuAdmin { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime? NgayDuyet { get; set; }
}

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public List<YeuCauViewModel> DanhSach { get; set; } = [];
    public int SoChoDuyet { get; set; }
    public string FilterTab { get; set; } = "cho_duyet";
    public string? ThongBao { get; set; }
    public string? LoiMsg { get; set; }

    private static readonly Dictionary<string, (decimal Gia, int SoNgay)> Goi = new()
    {
        ["ngay"] = (29_000m, 1),
        ["tuan"] = (99_000m, 7),
        ["thang"] = (199_000m, 30),
        ["nam"] = (999_000m, 365),
    };

    public async Task OnGetAsync([FromQuery] string? tab, [FromQuery] string? msg, [FromQuery] string? err)
    {
        ThongBao = msg;
        LoiMsg = err;
        FilterTab = tab ?? "cho_duyet";

        try
        {
            SoChoDuyet = await _db.YeuCauThanhToans.CountAsync(y => y.TrangThai == "cho_duyet");

            DanhSach = await _db.YeuCauThanhToans
                .AsNoTracking()
                .Where(y => y.TrangThai == FilterTab)
                .OrderByDescending(y => y.NgayTao)
                .Select(y => new YeuCauViewModel
                {
                    Id = y.Id,
                    MaThietBi = y.MaThietBi,
                    LoaiGoi = y.LoaiGoi,
                    SoTien = y.SoTien,
                    NoiDungChuyen = y.NoiDungChuyen,
                    TrangThai = y.TrangThai,
                    GhiChuAdmin = y.GhiChuAdmin,
                    NgayTao = y.NgayTao,
                    NgayDuyet = y.NgayDuyet
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            SoChoDuyet = 0;
            DanhSach = [];
            LoiMsg ??= $"Khong the tai danh sach duyet thanh toan: {ex.GetBaseException().Message}";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid yeuCauId)
    {
        try
        {
            var yc = await _db.YeuCauThanhToans.FirstOrDefaultAsync(y => y.Id == yeuCauId);
            if (yc == null)
                return RedirectToPage(new { err = "Khong tim thay yeu cau." });

            if (yc.TrangThai != "cho_duyet")
                return RedirectToPage(new { tab = yc.TrangThai, err = $"Yeu cau da o trang thai '{yc.TrangThai}'." });

            if (!Goi.TryGetValue(yc.LoaiGoi, out var info))
                return RedirectToPage(new { err = "Loai goi khong hop le." });

            var now = DateTime.UtcNow;

            var goiHienTai = await _db.DangKyApps
                .Where(d => d.MaThietBi == yc.MaThietBi && d.NgayHetHan > now)
                .OrderByDescending(d => d.NgayHetHan)
                .FirstOrDefaultAsync();

            var mocBatDau = goiHienTai?.NgayHetHan ?? now;
            var hetHan = mocBatDau.AddDays(info.SoNgay);

            _db.DangKyApps.Add(new DangKyApp
            {
                Id = Guid.NewGuid(),
                MaThietBi = yc.MaThietBi,
                LoaiGoi = yc.LoaiGoi,
                NgayBatDau = now,
                NgayHetHan = hetHan,
                SoTien = yc.SoTien
            });

            yc.TrangThai = "da_duyet";
            yc.NgayDuyet = now;
            await _db.SaveChangesAsync();

            return RedirectToPage(new
            {
                tab = "da_duyet",
                msg = $"Da duyet goi {yc.LoaiGoi} cho thiet bi. Het han: {hetHan:dd/MM/yyyy}"
            });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { tab = "cho_duyet", err = $"Khong the duyet yeu cau: {ex.GetBaseException().Message}" });
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid yeuCauId, string lyDo)
    {
        try
        {
            var yc = await _db.YeuCauThanhToans.FirstOrDefaultAsync(y => y.Id == yeuCauId);
            if (yc == null)
                return RedirectToPage(new { err = "Khong tim thay yeu cau." });

            if (yc.TrangThai != "cho_duyet")
                return RedirectToPage(new { tab = yc.TrangThai, err = $"Yeu cau da o trang thai '{yc.TrangThai}'." });

            yc.TrangThai = "tu_choi";
            yc.NgayDuyet = DateTime.UtcNow;
            yc.GhiChuAdmin = lyDo;
            await _db.SaveChangesAsync();

            return RedirectToPage(new { tab = "tu_choi", msg = "Da tu choi yeu cau." });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { tab = "cho_duyet", err = $"Khong the tu choi yeu cau: {ex.GetBaseException().Message}" });
        }
    }
}
