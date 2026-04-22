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
    private static readonly TimeSpan ExpiringSoonWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan ActivityStatsTimeout = TimeSpan.FromSeconds(8);
    private const int ViewedPoiExperience = 50;
    private const int VisitedPoiExperience = 100;
    private const int ExperiencePerLevel = 500;

    // Sources the app sends when the user physically walks into a POI (GPS/geofence).
    private static readonly string[] VisitedSourceValues = ["GPS", "APP-GEOFENCE", "APP_GEOFENCE", "GEOFENCE"];

    // Sources recorded when the user opens the POI detail screen.
    private static readonly string[] ViewedSourceValues = ["VIEW"];

    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    // Rows shown to admin after search/filter/sort.
    public List<CustomerActivityRow> Customers { get; private set; } = [];

    // Stats are always computed from the full customer base, never the filtered view.
    public int TotalCustomers { get; private set; }
    public int OnlineCustomers { get; private set; }
    public int ActiveSubscriptionCustomers { get; private set; }
    public int ExpiringSoonCustomers { get; private set; }

    public int DisplayedCustomers => Customers.Count;
    public string? ErrorMessage { get; private set; }

    public string Search { get; private set; } = "";
    public string Filter { get; private set; } = "all";
    public string SortBy { get; private set; } = "last";
    public string SortDir { get; private set; } = "desc";

    public async Task OnGetAsync(
        [FromQuery] string? search,
        [FromQuery] string? filter,
        [FromQuery] string? sort,
        [FromQuery] string? dir)
    {
        Search = (search ?? "").Trim();
        Filter = NormalizeOption(filter, "all");
        SortBy = NormalizeOption(sort, "last");
        SortDir = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

        try
        {
            var now = DateTime.UtcNow;
            var onlineCutoff = now.Subtract(OnlineThreshold);
            var expiringSoonCutoff = now.Add(ExpiringSoonWindow);

            var subscriptions = await LoadSubscriptionsAsync();
            var locations = await LoadLocationsAsync();
            var visitedCounts = await LoadPoiActivityCountsAsync(VisitedSourceValues, "lượt ghé");
            var viewedCounts = await LoadPoiActivityCountsAsync(ViewedSourceValues, "lượt xem");

            var rows = BuildCustomerRows(subscriptions, locations, visitedCounts, viewedCounts, now, onlineCutoff);

            // Stats always reflect the full customer base, not the filter.
            TotalCustomers = rows.Count;
            OnlineCustomers = rows.Count(x => x.IsOnline);
            ActiveSubscriptionCustomers = rows.Count(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value >= now);
            ExpiringSoonCustomers = rows.Count(x =>
                x.ExpiresAt.HasValue &&
                x.ExpiresAt.Value >= now &&
                x.ExpiresAt.Value <= expiringSoonCutoff);

            Customers = ApplySort(ApplyFilter(ApplySearch(rows), now, onlineCutoff, expiringSoonCutoff), now).ToList();
        }
        catch (Exception ex)
        {
            Customers = [];
            TotalCustomers = 0;
            OnlineCustomers = 0;
            ActiveSubscriptionCustomers = 0;
            ExpiringSoonCustomers = 0;
            AppendLoadWarning($"Không thể tải danh sách khách hàng: {ex.GetBaseException().Message}");
        }
    }

    // --- data loading -----------------------------------------------------

    private List<CustomerActivityRow> BuildCustomerRows(
        List<DeviceSubscription> subscriptions,
        List<DeviceLocation> locations,
        List<DevicePoiCount> visitedCounts,
        List<DevicePoiCount> viewedCounts,
        DateTime now,
        DateTime onlineCutoff)
    {
        var subscriptionMap = subscriptions
            .ToDictionary(x => x.DeviceId, StringComparer.OrdinalIgnoreCase);
        var locationMap = locations
            .Where(x => !string.IsNullOrWhiteSpace(x.MaThietBi))
            .GroupBy(x => x.MaThietBi, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.LanCuoiHeartbeat).First(),
                StringComparer.OrdinalIgnoreCase);
        var visitedMap = visitedCounts
            .ToDictionary(x => x.DeviceId, x => x.Count, StringComparer.OrdinalIgnoreCase);
        var viewedMap = viewedCounts
            .ToDictionary(x => x.DeviceId, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var deviceIds = subscriptionMap.Keys
            .Concat(locationMap.Keys)
            .Concat(visitedMap.Keys)
            .Concat(viewedMap.Keys)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return deviceIds.Select(deviceId =>
        {
            subscriptionMap.TryGetValue(deviceId, out var sub);
            locationMap.TryGetValue(deviceId, out var location);
            visitedMap.TryGetValue(deviceId, out var visitedCount);
            viewedMap.TryGetValue(deviceId, out var viewedCount);

            var expiresAt = sub?.ExpiresAt;
            var lastHeartbeat = location?.LanCuoiHeartbeat;
            var isOnline = lastHeartbeat.HasValue && lastHeartbeat.Value >= onlineCutoff;
            var currentPoi = isOnline && location?.PoiIdHienTai != null && !string.IsNullOrWhiteSpace(location.TenPoiHienTai)
                ? location.TenPoiHienTai
                : null;

            int? remainingDays = null;
            int? remainingHours = null;
            if (expiresAt.HasValue && expiresAt.Value >= now)
            {
                var delta = expiresAt.Value - now;
                remainingDays = (int)Math.Floor(delta.TotalDays);
                remainingHours = (int)Math.Floor(delta.TotalHours);
            }

            var experiencePoints = (viewedCount * ViewedPoiExperience) + (visitedCount * VisitedPoiExperience);
            var level = Math.Max(1, (experiencePoints / ExperiencePerLevel) + 1);
            var experienceInCurrentLevel = experiencePoints % ExperiencePerLevel;

            return new CustomerActivityRow
            {
                DeviceId = deviceId,
                DeviceShort = deviceId[..Math.Min(8, deviceId.Length)].ToUpperInvariant(),
                ExpiresAt = expiresAt,
                RemainingDays = remainingDays,
                RemainingHours = remainingHours,
                PaidPackagesCount = sub?.PaidPackages ?? 0,
                TotalSpent = sub?.TotalSpent ?? 0m,
                ViewedPoiCount = viewedCount,
                VisitedPoiCount = visitedCount,
                ExperiencePoints = experiencePoints,
                Level = level,
                ExperienceInCurrentLevel = experienceInCurrentLevel,
                ExperienceProgressPercent = (int)Math.Round(experienceInCurrentLevel * 100d / ExperiencePerLevel),
                CurrentPoiName = currentPoi,
                LastHeartbeat = lastHeartbeat,
                IsOnline = isOnline,
                IsAtPoi = currentPoi != null
            };
        }).ToList();
    }

    private async Task<List<DevicePoiCount>> LoadPoiActivityCountsAsync(string[] sourceValues, string label)
        => await ExecuteRawReadAsync(label, async (connection, ct) =>
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
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
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
        => await ExecuteRawReadAsync("gói đăng ký", async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = (int)ActivityStatsTimeout.TotalSeconds;
            command.CommandText = """
                SELECT
                    mathietbi,
                    max(ngayhethan) AS expires_at,
                    count(*) FILTER (WHERE sotien > 0)::int AS paid_packages,
                    coalesce(sum(sotien) FILTER (WHERE sotien > 0), 0) AS total_spent
                FROM dangkyapp
                WHERE mathietbi IS NOT NULL
                GROUP BY mathietbi
                """;

            var subscriptions = new List<DeviceSubscription>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                subscriptions.Add(new DeviceSubscription
                {
                    DeviceId = reader.GetString(0),
                    ExpiresAt = reader.GetDateTime(1),
                    PaidPackages = reader.GetInt32(2),
                    TotalSpent = reader.GetDecimal(3)
                });
            }

            return subscriptions;
        });

    private async Task<List<DeviceLocation>> LoadLocationsAsync()
        => await ExecuteRawReadAsync("vị trí khách hàng", async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = (int)ActivityStatsTimeout.TotalSeconds;
            command.CommandText = """
                SELECT mathietbi, lancuoi_heartbeat, poiid_hientai, ten_poi_hientai
                FROM vitrikhach
                WHERE mathietbi IS NOT NULL
                """;

            var locations = new List<DeviceLocation>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
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

    // --- search / filter / sort -------------------------------------------

    private IEnumerable<CustomerActivityRow> ApplySearch(IEnumerable<CustomerActivityRow> customers)
    {
        if (string.IsNullOrWhiteSpace(Search))
            return customers;

        return customers.Where(x =>
            x.DeviceId.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
            x.DeviceShort.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(x.CurrentPoiName) &&
                x.CurrentPoiName.Contains(Search, StringComparison.OrdinalIgnoreCase)));
    }

    private IEnumerable<CustomerActivityRow> ApplyFilter(
        IEnumerable<CustomerActivityRow> customers,
        DateTime now,
        DateTime onlineCutoff,
        DateTime expiringSoonCutoff)
        => Filter switch
        {
            "online" => customers.Where(x => x.IsOnline),
            "offline" => customers.Where(x => !x.IsOnline),
            "at-poi" => customers.Where(x => x.IsAtPoi),
            "active-sub" => customers.Where(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value >= now),
            "expiring-soon" => customers.Where(x =>
                x.ExpiresAt.HasValue &&
                x.ExpiresAt.Value >= now &&
                x.ExpiresAt.Value <= expiringSoonCutoff),
            "expired" => customers.Where(x => x.ExpiresAt.HasValue && x.ExpiresAt.Value < now),
            "no-sub" => customers.Where(x => !x.ExpiresAt.HasValue),
            _ => customers
        };

    private IEnumerable<CustomerActivityRow> ApplySort(
        IEnumerable<CustomerActivityRow> customers, DateTime now)
    {
        var descending = SortDir == "desc";
        IOrderedEnumerable<CustomerActivityRow> ordered = SortBy switch
        {
            "expiry" => descending
                ? customers.OrderByDescending(x => x.ExpiresAt ?? DateTime.MaxValue)
                : customers.OrderBy(x => x.ExpiresAt ?? DateTime.MaxValue),
            "experience" => descending
                ? customers.OrderByDescending(x => x.ExperiencePoints)
                : customers.OrderBy(x => x.ExperiencePoints),
            "visited" => descending
                ? customers.OrderByDescending(x => x.VisitedPoiCount)
                : customers.OrderBy(x => x.VisitedPoiCount),
            "viewed" => descending
                ? customers.OrderByDescending(x => x.ViewedPoiCount)
                : customers.OrderBy(x => x.ViewedPoiCount),
            "spent" => descending
                ? customers.OrderByDescending(x => x.TotalSpent)
                : customers.OrderBy(x => x.TotalSpent),
            _ => descending
                ? customers.OrderByDescending(x => x.LastHeartbeat ?? DateTime.MinValue)
                : customers.OrderBy(x => x.LastHeartbeat ?? DateTime.MinValue)
        };

        return ordered
            .ThenByDescending(x => x.IsOnline)
            .ThenBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeOption(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    // --- view helpers (shared between cshtml and code) --------------------

    public static string DescribeRemaining(CustomerActivityRow row)
    {
        if (!row.ExpiresAt.HasValue)
            return "Chưa mua gói";

        if (row.ExpiresAt.Value < DateTime.UtcNow)
            return "Đã hết hạn";

        if (row.RemainingDays is >= 1)
            return $"Còn {row.RemainingDays} ngày";

        if (row.RemainingHours is >= 1)
            return $"Còn {row.RemainingHours} giờ";

        return "Sắp hết trong 1 giờ";
    }

    public static string SubscriptionBadgeClass(CustomerActivityRow row)
    {
        if (!row.ExpiresAt.HasValue)
            return "sub-none";

        if (row.ExpiresAt.Value < DateTime.UtcNow)
            return "sub-expired";

        if (row.RemainingDays is <= 7)
            return "sub-warning";

        return "sub-active";
    }

    public static string ActivityBadgeClass(CustomerActivityRow row)
    {
        if (row.IsAtPoi)
            return "status-at-poi";

        if (row.IsOnline)
            return "status-online";

        if (row.LastHeartbeat.HasValue)
            return "status-offline";

        return "status-empty";
    }

    public static string ActivityBadgeText(CustomerActivityRow row)
    {
        if (row.IsAtPoi)
            return "Đang ở quán";

        if (row.IsOnline)
            return "Đang online";

        if (row.LastHeartbeat.HasValue)
            return "Ngoại tuyến";

        return "Chưa heartbeat";
    }

    public sealed class CustomerActivityRow
    {
        public string DeviceId { get; init; } = "";
        public string DeviceShort { get; init; } = "";
        public DateTime? ExpiresAt { get; init; }
        public int? RemainingDays { get; init; }
        public int? RemainingHours { get; init; }
        public int PaidPackagesCount { get; init; }
        public decimal TotalSpent { get; init; }
        public int ViewedPoiCount { get; init; }
        public int VisitedPoiCount { get; init; }
        public int ExperiencePoints { get; init; }
        public int Level { get; init; }
        public int ExperienceInCurrentLevel { get; init; }
        public int ExperienceProgressPercent { get; init; }
        public string? CurrentPoiName { get; init; }
        public DateTime? LastHeartbeat { get; init; }
        public bool IsOnline { get; init; }
        public bool IsAtPoi { get; init; }
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
        public int PaidPackages { get; init; }
        public decimal TotalSpent { get; init; }
    }

    private sealed class DeviceLocation
    {
        public string MaThietBi { get; init; } = "";
        public DateTime LanCuoiHeartbeat { get; init; }
        public Guid? PoiIdHienTai { get; init; }
        public string? TenPoiHienTai { get; init; }
    }
}
