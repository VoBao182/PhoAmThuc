using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.BanDo;

public class IndexModel : PageModel
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(2);

    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public List<CustomerActivityRow> Customers { get; private set; } = [];
    public int TotalCustomers { get; private set; }
    public int ActiveCustomers { get; private set; }
    public int CustomersAtPoi { get; private set; }
    public int ExpiredCustomers { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var onlineCutoff = now.Subtract(OnlineThreshold);

            var subscriptions = await _db.DangKyApps
                .AsNoTracking()
                .GroupBy(x => x.MaThietBi)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    ExpiresAt = g.Max(x => x.NgayHetHan)
                })
                .ToListAsync();

            var locations = await _db.VitriKhachs
                .AsNoTracking()
                .ToListAsync();

            var visitedCounts = await _db.LichSuPhats
                .AsNoTracking()
                .Where(x => x.MaThietBi != null && x.POIId != null && x.Nguon == "GPS")
                .GroupBy(x => x.MaThietBi!)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Count = g.Select(x => x.POIId).Distinct().Count()
                })
                .ToListAsync();

            var viewedCounts = await _db.LichSuPhats
                .AsNoTracking()
                .Where(x => x.MaThietBi != null && x.POIId != null && x.Nguon == "VIEW")
                .GroupBy(x => x.MaThietBi!)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Count = g.Select(x => x.POIId).Distinct().Count()
                })
                .ToListAsync();

            var subscriptionMap = subscriptions.ToDictionary(x => x.DeviceId, x => x.ExpiresAt);
            var locationMap = locations.ToDictionary(x => x.MaThietBi, x => x);
            var visitedMap = visitedCounts.ToDictionary(x => x.DeviceId, x => x.Count);
            var viewedMap = viewedCounts.ToDictionary(x => x.DeviceId, x => x.Count);

            var deviceIds = subscriptions.Select(x => x.DeviceId)
                .Concat(locations.Select(x => x.MaThietBi))
                .Concat(visitedCounts.Select(x => x.DeviceId))
                .Concat(viewedCounts.Select(x => x.DeviceId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Customers = deviceIds
                .Select(deviceId =>
                {
                    subscriptionMap.TryGetValue(deviceId, out var expiresAt);
                    locationMap.TryGetValue(deviceId, out var location);
                    visitedMap.TryGetValue(deviceId, out var visitedCount);
                    viewedMap.TryGetValue(deviceId, out var viewedCount);

                    var lastHeartbeat = location?.LanCuoiHeartbeat;
                    var isOnline = lastHeartbeat.HasValue && lastHeartbeat.Value >= onlineCutoff;
                    var isAtPoi = isOnline && location?.PoiIdHienTai != null && !string.IsNullOrWhiteSpace(location.TenPoiHienTai);
                    var isExpired = expiresAt != default && expiresAt < now;
                    var hasSubscription = expiresAt != default;

                    var remainingDays = hasSubscription && expiresAt >= now
                        ? Math.Max(1, (int)(expiresAt - now).TotalDays + 1)
                        : (int?)null;

                    return new CustomerActivityRow
                    {
                        DeviceId = deviceId,
                        DeviceShort = deviceId[..Math.Min(8, deviceId.Length)].ToUpperInvariant(),
                        ExpiresAt = hasSubscription ? expiresAt : null,
                        RemainingDays = remainingDays,
                        ViewedPoiCount = viewedCount,
                        VisitedPoiCount = visitedCount,
                        CurrentPoiName = isAtPoi ? location!.TenPoiHienTai : null,
                        LastHeartbeat = lastHeartbeat,
                        StatusText = GetStatusText(isOnline, isAtPoi, lastHeartbeat),
                        StatusClass = GetStatusClass(isOnline, isAtPoi, lastHeartbeat),
                        SubscriptionText = GetSubscriptionText(hasSubscription, isExpired, remainingDays),
                        SubscriptionClass = GetSubscriptionClass(hasSubscription, isExpired, remainingDays)
                    };
                })
                .OrderByDescending(x => x.LastHeartbeat.HasValue && x.LastHeartbeat.Value >= onlineCutoff)
                .ThenByDescending(x => x.CurrentPoiName != null)
                .ThenByDescending(x => x.ExpiresAt)
                .ThenBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            TotalCustomers = Customers.Count;
            ActiveCustomers = Customers.Count(x => x.LastHeartbeat.HasValue && x.LastHeartbeat.Value >= onlineCutoff);
            CustomersAtPoi = Customers.Count(x => x.CurrentPoiName != null);
            ExpiredCustomers = Customers.Count(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value < now);
        }
        catch (Exception ex)
        {
            Customers = [];
            TotalCustomers = 0;
            ActiveCustomers = 0;
            CustomersAtPoi = 0;
            ExpiredCustomers = 0;
            ErrorMessage = $"Không thể tải danh sách khách hàng: {ex.GetBaseException().Message}";
        }
    }

    private static string GetStatusText(bool isOnline, bool isAtPoi, DateTime? lastHeartbeat)
    {
        if (isAtPoi)
            return "Đang ở quán";

        if (isOnline)
            return "Đang hoạt động";

        if (lastHeartbeat.HasValue)
            return "Không hoạt động";

        return "Chưa ghi nhận";
    }

    private static string GetStatusClass(bool isOnline, bool isAtPoi, DateTime? lastHeartbeat)
    {
        if (isAtPoi)
            return "status-at-poi";

        if (isOnline)
            return "status-online";

        if (lastHeartbeat.HasValue)
            return "status-offline";

        return "status-empty";
    }

    private static string GetSubscriptionText(bool hasSubscription, bool isExpired, int? remainingDays)
    {
        if (!hasSubscription)
            return "Chưa đăng ký";

        if (isExpired)
            return "Hết hạn";

        return remainingDays == 1 ? "Còn 1 ngày" : $"Còn {remainingDays} ngày";
    }

    private static string GetSubscriptionClass(bool hasSubscription, bool isExpired, int? remainingDays)
    {
        if (!hasSubscription || isExpired)
            return "sub-expired";

        if (remainingDays <= 3)
            return "sub-warning";

        return "sub-active";
    }

    public sealed class CustomerActivityRow
    {
        public string DeviceId { get; init; } = "";
        public string DeviceShort { get; init; } = "";
        public DateTime? ExpiresAt { get; init; }
        public int? RemainingDays { get; init; }
        public int ViewedPoiCount { get; init; }
        public int VisitedPoiCount { get; init; }
        public string? CurrentPoiName { get; init; }
        public DateTime? LastHeartbeat { get; init; }
        public string StatusText { get; init; } = "";
        public string StatusClass { get; init; } = "";
        public string SubscriptionText { get; init; } = "";
        public string SubscriptionClass { get; init; } = "";
    }
}
