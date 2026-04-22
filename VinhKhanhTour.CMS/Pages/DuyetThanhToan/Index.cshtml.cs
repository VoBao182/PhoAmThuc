using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages.DuyetThanhToan;

public class YeuCauViewModel
{
    public Guid Id { get; set; }
    public string MaThietBi { get; set; } = "";
    public string DeviceShort { get; set; } = "";
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
    public string LatestPendingId { get; set; } = "";
    public string FilterTab { get; set; } = "cho_duyet";
    public string Search { get; set; } = "";
    public string PackageFilter { get; set; } = "all";
    public string? ThongBao { get; set; }
    public string? LoiMsg { get; set; }

    // Stats across all requests, not limited by tab / search / paging.
    public int SoChoDuyet { get; set; }
    public int SoDaDuyet { get; set; }
    public int SoTuChoi { get; set; }
    public int TongYeuCau => SoChoDuyet + SoDaDuyet + SoTuChoi;
    public decimal TienChoDuyet { get; set; }
    public decimal DoanhThuDaDuyet { get; set; }

    public int DisplayedCount => DanhSach.Count;

    private static readonly string[] KnownPackages = ["ngay", "tuan", "thang", "nam"];

    private static readonly Dictionary<string, (decimal Gia, int SoNgay)> Goi = new()
    {
        ["ngay"] = (29_000m, 1),
        ["tuan"] = (99_000m, 7),
        ["thang"] = (199_000m, 30),
        ["nam"] = (999_000m, 365),
    };

    public async Task OnGetAsync(
        [FromQuery] string? tab,
        [FromQuery] string? search,
        [FromQuery] string? pkg,
        [FromQuery] string? msg,
        [FromQuery] string? err)
    {
        ThongBao = msg;
        LoiMsg = err;
        FilterTab = NormalizeTab(tab);
        Search = (search ?? "").Trim();
        PackageFilter = NormalizePackage(pkg);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await LoadStatsAsync();
                await LoadListAsync();
                return;
            }
            catch (Exception ex) when (attempt == 0 && IsDisposedWaitHandle(ex))
            {
                ClearNpgsqlPoolsQuietly();
            }
            catch (Exception ex)
            {
                DanhSach = [];
                SoChoDuyet = 0;
                SoDaDuyet = 0;
                SoTuChoi = 0;
                TienChoDuyet = 0m;
                DoanhThuDaDuyet = 0m;
                LatestPendingId = "";
                LoiMsg ??= IsDisposedWaitHandle(ex)
                    ? "Tạm thời chưa tải được danh sách duyệt thanh toán. Hãy tải lại trang sau vài giây."
                    : $"Không thể tải danh sách duyệt thanh toán: {ex.GetBaseException().Message}";
                return;
            }
        }

        DanhSach = [];
        LoiMsg ??= "Tạm thời chưa tải được danh sách duyệt thanh toán. Hãy tải lại trang sau vài giây.";
    }

    private async Task LoadStatsAsync()
    {
        var statusCounts = await _db.YeuCauThanhToans
            .AsNoTracking()
            .GroupBy(y => y.TrangThai)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(x => (decimal?)x.SoTien) ?? 0m })
            .ToListAsync();

        foreach (var row in statusCounts)
        {
            switch (row.Status)
            {
                case "cho_duyet":
                    SoChoDuyet = row.Count;
                    TienChoDuyet = row.Amount;
                    break;
                case "da_duyet":
                    SoDaDuyet = row.Count;
                    DoanhThuDaDuyet = row.Amount;
                    break;
                case "tu_choi":
                    SoTuChoi = row.Count;
                    break;
            }
        }

        var latestPendingId = await _db.YeuCauThanhToans
            .AsNoTracking()
            .Where(y => y.TrangThai == "cho_duyet")
            .OrderByDescending(y => y.NgayTao)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync();
        LatestPendingId = latestPendingId?.ToString("N") ?? "";
    }

    private async Task LoadListAsync()
    {
        var query = _db.YeuCauThanhToans
            .AsNoTracking()
            .Where(y => y.TrangThai == FilterTab);

        if (PackageFilter != "all")
            query = query.Where(y => y.LoaiGoi == PackageFilter);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.ToLower();
            query = query.Where(y =>
                y.MaThietBi.ToLower().Contains(s) ||
                y.NoiDungChuyen.ToLower().Contains(s));
        }

        DanhSach = await query
            .OrderByDescending(y => y.NgayTao)
            .Take(200)
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

        foreach (var item in DanhSach)
        {
            item.DeviceShort = item.MaThietBi.Length > 8
                ? item.MaThietBi[..8].ToUpperInvariant()
                : item.MaThietBi.ToUpperInvariant();
        }
    }

    public async Task<JsonResult> OnGetPendingSnapshotAsync()
    {
        try
        {
            var pendingQuery = _db.YeuCauThanhToans
                .AsNoTracking()
                .Where(y => y.TrangThai == "cho_duyet");

            var count = await pendingQuery.CountAsync();
            var latest = await pendingQuery
                .OrderByDescending(y => y.NgayTao)
                .Select(y => new { y.Id, y.NgayTao, y.NoiDungChuyen, y.MaThietBi })
                .FirstOrDefaultAsync();

            var latestDeviceShort = "";
            if (!string.IsNullOrWhiteSpace(latest?.MaThietBi))
            {
                latestDeviceShort = latest.MaThietBi.Length > 8
                    ? latest.MaThietBi[..8].ToUpperInvariant()
                    : latest.MaThietBi.ToUpperInvariant();
            }

            return new JsonResult(new
            {
                Count = count,
                LatestId = latest == null ? "" : latest.Id.ToString("N"),
                LatestCreatedAt = latest?.NgayTao,
                LatestTransferContent = latest?.NoiDungChuyen ?? "",
                LatestDeviceShort = latestDeviceShort,
                Ok = true
            });
        }
        catch (Exception ex)
        {
            if (IsDisposedWaitHandle(ex))
                ClearNpgsqlPoolsQuietly();

            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return new JsonResult(new
            {
                Count = SoChoDuyet,
                LatestId = LatestPendingId,
                LatestCreatedAt = (DateTime?)null,
                LatestTransferContent = "",
                LatestDeviceShort = "",
                Ok = false,
                Error = ex.GetBaseException().Message
            });
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid yeuCauId)
    {
        try
        {
            var yc = await _db.YeuCauThanhToans.FirstOrDefaultAsync(y => y.Id == yeuCauId);
            if (yc == null)
                return RedirectToPage(new { err = "Không tìm thấy yêu cầu." });

            if (yc.TrangThai != "cho_duyet")
                return RedirectToPage(new { tab = yc.TrangThai, err = $"Yêu cầu đã ở trạng thái '{yc.TrangThai}'." });

            if (!Goi.TryGetValue(yc.LoaiGoi, out var info))
                return RedirectToPage(new { err = "Loại gói không hợp lệ." });

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
                msg = $"Đã duyệt gói {yc.LoaiGoi} cho thiết bị. Hết hạn: {hetHan:dd/MM/yyyy}"
            });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { tab = "cho_duyet", err = $"Không thể duyệt yêu cầu: {ex.GetBaseException().Message}" });
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid yeuCauId, string lyDo)
    {
        try
        {
            var yc = await _db.YeuCauThanhToans.FirstOrDefaultAsync(y => y.Id == yeuCauId);
            if (yc == null)
                return RedirectToPage(new { err = "Không tìm thấy yêu cầu." });

            if (yc.TrangThai != "cho_duyet")
                return RedirectToPage(new { tab = yc.TrangThai, err = $"Yêu cầu đã ở trạng thái '{yc.TrangThai}'." });

            yc.TrangThai = "tu_choi";
            yc.NgayDuyet = DateTime.UtcNow;
            yc.GhiChuAdmin = lyDo;
            await _db.SaveChangesAsync();

            return RedirectToPage(new { tab = "tu_choi", msg = "Đã từ chối yêu cầu." });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { tab = "cho_duyet", err = $"Không thể từ chối yêu cầu: {ex.GetBaseException().Message}" });
        }
    }

    private static string NormalizeTab(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "cho_duyet" : value.Trim().ToLowerInvariant();
        return normalized is "cho_duyet" or "da_duyet" or "tu_choi" ? normalized : "cho_duyet";
    }

    private static string NormalizePackage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "all";

        var normalized = value.Trim().ToLowerInvariant();
        return KnownPackages.Contains(normalized) ? normalized : "all";
    }

    public static string DescribePackage(string code) => code switch
    {
        "ngay" => "1 ngày",
        "tuan" => "1 tuần",
        "thang" => "1 tháng",
        "nam" => "1 năm",
        _ => code
    };

    public static string DescribeRelativeTime(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalSeconds < 60)
            return "vừa xong";
        if (delta.TotalMinutes < 60)
            return $"{(int)delta.TotalMinutes} phút trước";
        if (delta.TotalHours < 24)
            return $"{(int)delta.TotalHours} giờ trước";
        if (delta.TotalDays < 7)
            return $"{(int)delta.TotalDays} ngày trước";
        return utc.ToLocalTime().ToString("dd/MM/yyyy");
    }

    private static bool IsDisposedWaitHandle(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is ObjectDisposedException od &&
                string.Equals(od.ObjectName, "System.Threading.ManualResetEventSlim", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void ClearNpgsqlPoolsQuietly()
    {
        try
        {
            NpgsqlConnection.ClearAllPools();
        }
        catch
        {
        }
    }
}
