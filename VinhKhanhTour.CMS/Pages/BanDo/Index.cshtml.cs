using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.BanDo;

public class IndexModel : PageModel
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(2);
    private const int ViewedPoiExperience = 50;
    private const int VisitedPoiExperience = 100;
    private const int ExperiencePerLevel = 500;
    private static readonly string[] VisitedSourceValues = ["GPS", "APP-GEOFENCE", "APP_GEOFENCE", "GEOFENCE"];
    private static readonly string[] ViewedSourceValues = ["VIEW"];

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
    public int TotalExperiencePoints { get; private set; }
    public int HighestCustomerLevel { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string Search { get; private set; } = "";
    public string StatusFilter { get; private set; } = "all";
    public string ExpiryFilter { get; private set; } = "all";
    public string SortBy { get; private set; } = "last";
    public string SortDir { get; private set; } = "desc";

    public async Task OnGetAsync(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? expiry,
        [FromQuery] string? sort,
        [FromQuery] string? dir)
    {
        Search = (search ?? "").Trim();
        StatusFilter = NormalizeOption(status, "all");
        ExpiryFilter = NormalizeOption(expiry, "all");
        SortBy = NormalizeOption(sort, "last");
        SortDir = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

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
                .Where(x => x.MaThietBi != null
                    && x.POIId != null
                    && x.Nguon != null
                    && VisitedSourceValues.Contains(x.Nguon.ToUpper()))
                .GroupBy(x => x.MaThietBi!)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Count = g.Select(x => x.POIId).Distinct().Count()
                })
                .ToListAsync();

            var viewedCounts = await _db.LichSuPhats
                .AsNoTracking()
                .Where(x => x.MaThietBi != null
                    && x.POIId != null
                    && x.Nguon != null
                    && ViewedSourceValues.Contains(x.Nguon.ToUpper()))
                .GroupBy(x => x.MaThietBi!)
                .Select(g => new
                {
                    DeviceId = g.Key,
                    Count = g.Select(x => x.POIId).Distinct().Count()
                })
                .ToListAsync();

            var subscriptionMap = subscriptions
                .GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Max(x => x.ExpiresAt), StringComparer.OrdinalIgnoreCase);
            var locationMap = locations
                .Where(x => !string.IsNullOrWhiteSpace(x.MaThietBi))
                .GroupBy(x => x.MaThietBi, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.LanCuoiHeartbeat).First(),
                    StringComparer.OrdinalIgnoreCase);
            var visitedMap = visitedCounts
                .GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count), StringComparer.OrdinalIgnoreCase);
            var viewedMap = viewedCounts
                .GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count), StringComparer.OrdinalIgnoreCase);

            var deviceIds = subscriptions.Select(x => x.DeviceId)
                .Concat(locations.Select(x => x.MaThietBi))
                .Concat(visitedCounts.Select(x => x.DeviceId))
                .Concat(viewedCounts.Select(x => x.DeviceId))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rows = deviceIds
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
                    var experiencePoints = (viewedCount * ViewedPoiExperience) + (visitedCount * VisitedPoiExperience);
                    var level = Math.Max(1, (experiencePoints / ExperiencePerLevel) + 1);
                    var experienceInCurrentLevel = experiencePoints % ExperiencePerLevel;

                    return new CustomerActivityRow
                    {
                        DeviceId = deviceId,
                        DeviceShort = deviceId[..Math.Min(8, deviceId.Length)].ToUpperInvariant(),
                        ExpiresAt = hasSubscription ? expiresAt : null,
                        RemainingDays = remainingDays,
                        ViewedPoiCount = viewedCount,
                        VisitedPoiCount = visitedCount,
                        ExperiencePoints = experiencePoints,
                        Level = level,
                        ExperienceInCurrentLevel = experienceInCurrentLevel,
                        ExperienceProgressPercent = (int)Math.Round(experienceInCurrentLevel * 100d / ExperiencePerLevel),
                        CurrentPoiName = isAtPoi ? location!.TenPoiHienTai : null,
                        LastHeartbeat = lastHeartbeat,
                        StatusText = GetStatusText(isOnline, isAtPoi, lastHeartbeat),
                        StatusClass = GetStatusClass(isOnline, isAtPoi, lastHeartbeat),
                        SubscriptionText = GetSubscriptionText(hasSubscription, isExpired, remainingDays),
                        SubscriptionClass = GetSubscriptionClass(hasSubscription, isExpired, remainingDays)
                    };
                })
                .ToList();

            Customers = ApplySort(ApplyExpiryFilter(ApplyStatusFilter(ApplySearchFilter(rows), now), now), now, onlineCutoff)
                .ToList();

            TotalCustomers = Customers.Count;
            ActiveCustomers = Customers.Count(x => x.LastHeartbeat.HasValue && x.LastHeartbeat.Value >= onlineCutoff);
            CustomersAtPoi = Customers.Count(x => x.CurrentPoiName != null);
            ExpiredCustomers = Customers.Count(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value < now);
            TotalExperiencePoints = Customers.Sum(x => x.ExperiencePoints);
            HighestCustomerLevel = Customers.Count == 0 ? 0 : Customers.Max(x => x.Level);
        }
        catch (Exception ex)
        {
            Customers = [];
            TotalCustomers = 0;
            ActiveCustomers = 0;
            CustomersAtPoi = 0;
            ExpiredCustomers = 0;
            TotalExperiencePoints = 0;
            HighestCustomerLevel = 0;
            ErrorMessage = $"Không thể tải danh sách khách hàng: {ex.GetBaseException().Message}";
        }
    }

    private static string NormalizeOption(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private IEnumerable<CustomerActivityRow> ApplySearchFilter(IEnumerable<CustomerActivityRow> customers)
    {
        if (string.IsNullOrWhiteSpace(Search))
            return customers;

        return customers.Where(x =>
            x.DeviceId.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
            x.DeviceShort.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(x.CurrentPoiName) && x.CurrentPoiName.Contains(Search, StringComparison.OrdinalIgnoreCase)));
    }

    private IEnumerable<CustomerActivityRow> ApplyStatusFilter(IEnumerable<CustomerActivityRow> customers, DateTime now)
        => StatusFilter switch
        {
            "online" => customers.Where(x => IsOnline(x)),
            "at-poi" => customers.Where(x => x.CurrentPoiName != null),
            "offline" => customers.Where(x => x.LastHeartbeat.HasValue && !IsOnline(x)),
            "never" => customers.Where(x => !x.LastHeartbeat.HasValue),
            "active-sub" => customers.Where(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value >= now),
            "expired" => customers.Where(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value < now),
            "no-sub" => customers.Where(x => !x.ExpiresAt.HasValue),
            _ => customers
        };

    private IEnumerable<CustomerActivityRow> ApplyExpiryFilter(IEnumerable<CustomerActivityRow> customers, DateTime now)
        => ExpiryFilter switch
        {
            "active" => customers.Where(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value >= now),
            "expiring7" => customers.Where(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value >= now && x.ExpiresAt.Value <= now.AddDays(7)),
            "expired" => customers.Where(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value < now),
            "none" => customers.Where(x => !x.ExpiresAt.HasValue),
            _ => customers
        };

    private IEnumerable<CustomerActivityRow> ApplySort(
        IEnumerable<CustomerActivityRow> customers,
        DateTime now,
        DateTime onlineCutoff)
    {
        var descending = SortDir == "desc";
        IOrderedEnumerable<CustomerActivityRow> ordered = SortBy switch
        {
            "expiry" => descending
                ? customers.OrderByDescending(x => GetRemainingDaysSortValue(x, now))
                : customers.OrderBy(x => GetRemainingDaysSortValue(x, now)),
            "experience" => descending
                ? customers.OrderByDescending(x => x.ExperiencePoints)
                : customers.OrderBy(x => x.ExperiencePoints),
            "viewed" => descending
                ? customers.OrderByDescending(x => x.ViewedPoiCount)
                : customers.OrderBy(x => x.ViewedPoiCount),
            "visited" => descending
                ? customers.OrderByDescending(x => x.VisitedPoiCount)
                : customers.OrderBy(x => x.VisitedPoiCount),
            "device" => descending
                ? customers.OrderByDescending(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                : customers.OrderBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase),
            "current" => descending
                ? customers.OrderByDescending(x => x.CurrentPoiName ?? "", StringComparer.CurrentCultureIgnoreCase)
                : customers.OrderBy(x => x.CurrentPoiName ?? "", StringComparer.CurrentCultureIgnoreCase),
            _ => descending
                ? customers.OrderByDescending(x => x.LastHeartbeat ?? DateTime.MinValue)
                : customers.OrderBy(x => x.LastHeartbeat ?? DateTime.MinValue)
        };

        return ordered
            .ThenByDescending(x => x.LastHeartbeat.HasValue && x.LastHeartbeat.Value >= onlineCutoff)
            .ThenByDescending(x => x.CurrentPoiName != null)
            .ThenBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsOnline(CustomerActivityRow customer)
        => customer.LastHeartbeat.HasValue && customer.LastHeartbeat.Value >= DateTime.UtcNow.Subtract(OnlineThreshold);

    private static int GetRemainingDaysSortValue(CustomerActivityRow customer, DateTime now)
    {
        if (!customer.ExpiresAt.HasValue)
            return int.MinValue;

        return (int)Math.Floor((customer.ExpiresAt.Value - now).TotalDays);
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
        public int ExperiencePoints { get; init; }
        public int Level { get; init; }
        public int ExperienceInCurrentLevel { get; init; }
        public int ExperienceProgressPercent { get; init; }
        public string? CurrentPoiName { get; init; }
        public DateTime? LastHeartbeat { get; init; }
        public string StatusText { get; init; } = "";
        public string StatusClass { get; init; } = "";
        public string SubscriptionText { get; init; } = "";
        public string SubscriptionClass { get; init; } = "";
    }
}
