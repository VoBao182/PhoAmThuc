using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using VinhKhanhTour.API.Data;

namespace VinhKhanhTour.CMS.Pages.BanDo;

public class IndexModel : PageModel
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ActivityStatsTimeout = TimeSpan.FromSeconds(8);
    private const int ViewedPoiExperience = 50;
    private const int VisitedPoiExperience = 100;
    private const int ExperiencePerLevel = 500;
    private static readonly string[] VisitedSourceValues = ["GPS", "APP-GEOFENCE", "APP_GEOFENCE", "GEOFENCE"];
    private static readonly string[] ViewedSourceValues = ["VIEW", "GPS", "APP-GEOFENCE", "APP_GEOFENCE", "GEOFENCE"];

    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public List<CustomerActivityRow> Customers { get; private set; } = [];
    public int SubscribedCustomers { get; private set; }
    public int DisplayedCustomers { get; private set; }
    public int TotalRecordedDevices { get; private set; }
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

            var subscriptions = await LoadSubscriptionsAsync();
            var locations = await LoadLocationsAsync();

            var visitedCounts = await LoadPoiActivityCountsAsync(VisitedSourceValues, "luot ghe");
            var viewedCounts = await LoadPoiActivityCountsAsync(ViewedSourceValues, "luot xem");

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
                        HasSubscription = hasSubscription,
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

            var allCustomers = rows.ToList();

            Customers = ApplySort(ApplyExpiryFilter(ApplyStatusFilter(ApplySearchFilter(allCustomers), now), now), now, onlineCutoff)
                .ToList();

            DisplayedCustomers = Customers.Count;
            TotalRecordedDevices = allCustomers.Count;
            SubscribedCustomers = allCustomers.Count(x => x.HasSubscription);
            ActiveCustomers = allCustomers.Count(x => x.LastHeartbeat.HasValue && x.LastHeartbeat.Value >= onlineCutoff);
            CustomersAtPoi = allCustomers.Count(x => x.CurrentPoiName != null);
            ExpiredCustomers = allCustomers.Count(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value < now);
            TotalExperiencePoints = allCustomers.Sum(x => x.ExperiencePoints);
            HighestCustomerLevel = allCustomers.Count == 0 ? 0 : allCustomers.Max(x => x.Level);
        }
        catch (Exception ex)
        {
            Customers = [];
            SubscribedCustomers = 0;
            DisplayedCustomers = 0;
            TotalRecordedDevices = 0;
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

    private async Task<List<DevicePoiCount>> LoadPoiActivityCountsAsync(string[] sourceValues, string label)
        => await ExecuteRawReadAsync(label, async (connection, cancellationToken) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = (int)ActivityStatsTimeout.TotalSeconds;
            command.CommandText = """
                SELECT mathietbi, count(DISTINCT poiid)::int
                FROM lichsuphat
                WHERE mathietbi IS NOT NULL
                  AND poiid IS NOT NULL
                  AND nguon = ANY (@sources)
                GROUP BY mathietbi
                """;
            command.Parameters.Add("sources", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = sourceValues;

            var counts = new List<DevicePoiCount>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                counts.Add(new DevicePoiCount
                {
                    DeviceId = reader.GetString(0),
                    Count = reader.GetInt32(1)
                });
            }

            return counts;
        });

    private async Task<List<DeviceSubscription>> LoadSubscriptionsAsync()
        => await ExecuteRawReadAsync("gói đăng ký", async (connection, cancellationToken) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = (int)ActivityStatsTimeout.TotalSeconds;
            command.CommandText = """
                SELECT mathietbi, max(ngayhethan)
                FROM dangkyapp
                WHERE mathietbi IS NOT NULL
                GROUP BY mathietbi
                """;

            var subscriptions = new List<DeviceSubscription>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                subscriptions.Add(new DeviceSubscription
                {
                    DeviceId = reader.GetString(0),
                    ExpiresAt = reader.GetDateTime(1)
                });
            }

            return subscriptions;
        });

    private async Task<List<DeviceLocation>> LoadLocationsAsync()
        => await ExecuteRawReadAsync("vi tri khach hang", async (connection, cancellationToken) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = (int)ActivityStatsTimeout.TotalSeconds;
            command.CommandText = """
                SELECT mathietbi, lancuoi_heartbeat, poiid_hientai, ten_poi_hientai
                FROM vitrikhach
                WHERE mathietbi IS NOT NULL
                """;

            var locations = new List<DeviceLocation>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                locations.Add(new DeviceLocation
                {
                    MaThietBi = reader.GetString(0),
                    LanCuoiHeartbeat = reader.GetDateTime(1),
                    PoiIdHienTai = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    TenPoiHienTai = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }

            return locations;
        });

    private async Task<List<T>> ExecuteRawReadAsync<T>(
        string label,
        Func<NpgsqlConnection, CancellationToken, Task<List<T>>> readAsync)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var timeout = new CancellationTokenSource(ActivityStatsTimeout);

            try
            {
                var connectionString = _db.Database.GetConnectionString()
                    ?? throw new InvalidOperationException("Missing database connection string.");

                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(timeout.Token);

                return await readAsync(connection, timeout.Token);
            }
            catch (Exception ex) when (attempt == 0 && IsDisposedWaitHandle(ex))
            {
                ClearNpgsqlPoolsQuietly();
            }
            catch (Exception ex)
            {
                AppendLoadWarning($"Tạm thời không tải được {label}: {ex.GetBaseException().Message}");
                return [];
            }
        }

        AppendLoadWarning($"Tạm thời không tải được {label}: kết nối Supabase đang bận.");
        return [];
    }

    private void AppendLoadWarning(string message)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(ErrorMessage)
            ? message
            : $"{ErrorMessage} {message}";
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
        public bool HasSubscription { get; init; }
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

    private sealed class DevicePoiCount
    {
        public string DeviceId { get; init; } = "";
        public int Count { get; init; }
    }

    private sealed class DeviceSubscription
    {
        public string DeviceId { get; init; } = "";
        public DateTime ExpiresAt { get; init; }
    }

    private sealed class DeviceLocation
    {
        public string MaThietBi { get; init; } = "";
        public DateTime LanCuoiHeartbeat { get; init; }
        public Guid? PoiIdHienTai { get; init; }
        public string? TenPoiHienTai { get; init; }
    }
}
