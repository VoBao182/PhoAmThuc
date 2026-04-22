using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(AppDbContext db, IConfiguration config, ILogger<IndexModel> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    // POI list + basic stats
    public List<POI> POIs { get; set; } = [];
    public int TongPOI { get; set; }
    public int TongMonAn { get; set; }
    public int SoQuanQuaHan { get; set; }
    public string ApiBaseUrl => _config["ApiBaseUrl"] ?? "http://localhost:5118";
    public string? ErrorMessage { get; private set; }

    // --- Analytics: range descriptor ---
    // Mode values:
    //   "today" | "last7" | "last30" | "last12m"   — rolling presets
    //   "day" | "week" | "month" | "year"           — absolute pickers (need ParamDate / ParamMonth / ParamYear)
    //   "custom"                                    — arbitrary from/to
    public string Mode { get; private set; } = "last7";
    public DateTime SinceUtc { get; private set; }
    public DateTime UntilUtc { get; private set; }
    public string Granularity { get; private set; } = "day";
    public string RangeLabel { get; private set; } = "7 ngày qua";

    // Values re-surfaced to the custom form so inputs keep their selection.
    public string ParamDate { get; private set; } = "";       // YYYY-MM-DD
    public string ParamWeek { get; private set; } = "";       // YYYY-Www (from ISO week)
    public string ParamMonth { get; private set; } = "";      // YYYY-MM
    public int ParamYear { get; private set; }
    public string ParamFrom { get; private set; } = "";
    public string ParamTo { get; private set; } = "";

    public int TotalActiveDevices { get; private set; }
    public int TotalVisits { get; private set; }
    public int TotalViews { get; private set; }
    public decimal TotalRevenue { get; private set; }
    public string? AnalyticsError { get; private set; }

    public List<TimeBucket> ActivityBuckets { get; private set; } = [];
    public List<GeoHeatPoint> GeoPoints { get; private set; } = [];
    public List<PoiStat> TopPoi { get; private set; } = [];
    public List<RevenueBucket> RevenueBuckets { get; private set; } = [];

    public string ActivityJson => Serialize(ActivityBuckets);
    public string GeoJson => Serialize(GeoPoints);
    public string RevenueJson => Serialize(RevenueBuckets);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task OnGetAsync(
        [FromQuery] string? mode,
        [FromQuery] string? date,
        [FromQuery] string? week,
        [FromQuery] string? month,
        [FromQuery] int? year,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        ResolveRange(mode, date, week, month, year, from, to);

        await LoadPoiSectionAsync();
        await LoadAnalyticsWithRetryAsync();
    }

    // ------------------------------------------------------------------
    // POI list section (kept as before, with retry guard)
    // ------------------------------------------------------------------
    private async Task LoadPoiSectionAsync()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                POIs = await _db.POIs
                    .AsNoTracking()
                    .Include(p => p.MonAns)
                    .OrderBy(p => p.MucUuTien)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                TongPOI = POIs.Count(p => p.TrangThai);
                TongMonAn = POIs.SelectMany(p => p.MonAns).Count(m => m.TinhTrang);
                SoQuanQuaHan = POIs.Count(p =>
                    p.TrangThai && (p.NgayHetHanDuyTri == null || p.NgayHetHanDuyTri < now));
                return;
            }
            catch (Exception ex) when (attempt == 0 && IsDisposedWaitHandle(ex))
            {
                ClearNpgsqlPoolsQuietly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load POIs from database on CMS home page");
                POIs = [];
                TongPOI = 0;
                TongMonAn = 0;
                SoQuanQuaHan = 0;
                ErrorMessage = IsDisposedWaitHandle(ex)
                    ? "Tạm thời chưa tải được danh sách POI. Hãy thử lại sau vài giây."
                    : $"Không thể tải dữ liệu từ database: {ex.GetBaseException().Message}";
                return;
            }
        }

        POIs = [];
        ErrorMessage ??= "Tạm thời chưa tải được danh sách POI. Hãy thử lại sau vài giây.";
    }

    // ------------------------------------------------------------------
    // Analytics section (4 aggregations on one shared EF connection)
    // ------------------------------------------------------------------
    private async Task LoadAnalyticsWithRetryAsync()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await LoadAnalyticsAsync();
                return;
            }
            catch (Exception ex) when (attempt == 0 && IsDisposedWaitHandle(ex))
            {
                ClearNpgsqlPoolsQuietly();
            }
            catch (Exception ex)
            {
                ResetAnalytics();
                AnalyticsError = IsDisposedWaitHandle(ex)
                    ? "Tạm thời chưa tải được dữ liệu thống kê. Hãy thử lại sau vài giây."
                    : $"Không thể tải thống kê: {ex.GetBaseException().Message}";
                return;
            }
        }

        ResetAnalytics();
        AnalyticsError ??= "Tạm thời chưa tải được dữ liệu thống kê. Hãy thử lại sau vài giây.";
    }

    private async Task LoadAnalyticsAsync()
    {
        var since = SinceUtc;
        var until = UntilUtc;
        var bucketUnit = Granularity;

        var connection = _db.Database.GetDbConnection();
        var closeConnection = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
            closeConnection = true;
        }

        try
        {
            await LoadActivityAsync(connection, since, until, bucketUnit);
            await LoadGeoAsync(connection, since, until);
            await LoadTopPoiAsync(connection, since, until);
            await LoadRevenueAsync(connection, since, until, bucketUnit);
            await LoadSummaryAsync(connection, since, until);
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }

        FillMissingBuckets(since, until, bucketUnit);
    }

    private async Task LoadActivityAsync(System.Data.Common.DbConnection conn, DateTime since, DateTime until, string bucketUnit)
    {
        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandTimeout = 15;
        cmd.CommandText = """
            SELECT date_trunc(@unit, thoigian) AS bucket,
                   COUNT(DISTINCT mathietbi) AS active_users,
                   SUM(CASE WHEN nguon = 'VIEW' THEN 1 ELSE 0 END) AS views,
                   SUM(CASE WHEN upper(nguon) IN ('GPS','APP-GEOFENCE','APP_GEOFENCE','GEOFENCE') THEN 1 ELSE 0 END) AS visits
            FROM lichsuphat
            WHERE thoigian >= @since AND thoigian < @until
              AND mathietbi IS NOT NULL
            GROUP BY bucket
            ORDER BY bucket
            """;
        cmd.Parameters.Add(new NpgsqlParameter("unit", NpgsqlDbType.Text) { Value = bucketUnit });
        cmd.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = since });
        cmd.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = until });

        ActivityBuckets.Clear();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ActivityBuckets.Add(new TimeBucket
            {
                BucketUtc = reader.GetDateTime(0),
                ActiveUsers = reader.GetInt32(1),
                Views = reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2),
                Visits = reader.IsDBNull(3) ? 0 : (int)reader.GetInt64(3)
            });
        }
    }

    private async Task LoadGeoAsync(System.Data.Common.DbConnection conn, DateTime since, DateTime until)
    {
        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandTimeout = 10;
        cmd.CommandText = """
            SELECT p.vido, p.kinhdo, p.tenpoi, COUNT(*) AS weight
            FROM lichsuphat l
            JOIN poi p ON p.id = l.poiid
            WHERE l.thoigian >= @since AND l.thoigian < @until AND l.poiid IS NOT NULL
            GROUP BY p.vido, p.kinhdo, p.tenpoi
            ORDER BY weight DESC
            LIMIT 500
            """;
        cmd.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = since });
        cmd.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = until });

        GeoPoints.Clear();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            GeoPoints.Add(new GeoHeatPoint
            {
                Lat = reader.GetDouble(0),
                Lng = reader.GetDouble(1),
                Name = reader.GetString(2),
                Weight = (int)reader.GetInt64(3)
            });
        }
    }

    private async Task LoadTopPoiAsync(System.Data.Common.DbConnection conn, DateTime since, DateTime until)
    {
        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandTimeout = 10;
        cmd.CommandText = """
            SELECT p.id, p.tenpoi,
                   SUM(CASE WHEN l.nguon = 'VIEW' THEN 1 ELSE 0 END) AS views,
                   SUM(CASE WHEN upper(l.nguon) IN ('GPS','APP-GEOFENCE','APP_GEOFENCE','GEOFENCE') THEN 1 ELSE 0 END) AS visits
            FROM poi p
            LEFT JOIN lichsuphat l ON l.poiid = p.id AND l.thoigian >= @since AND l.thoigian < @until
            GROUP BY p.id, p.tenpoi
            ORDER BY (COALESCE(SUM(CASE WHEN l.nguon = 'VIEW' THEN 1 ELSE 0 END), 0)
                    + COALESCE(SUM(CASE WHEN upper(l.nguon) IN ('GPS','APP-GEOFENCE','APP_GEOFENCE','GEOFENCE') THEN 1 ELSE 0 END), 0)) DESC
            LIMIT 10
            """;
        cmd.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = since });
        cmd.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = until });

        TopPoi.Clear();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var views = reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2);
            var visits = reader.IsDBNull(3) ? 0 : (int)reader.GetInt64(3);
            TopPoi.Add(new PoiStat
            {
                PoiId = reader.GetGuid(0),
                Name = reader.GetString(1),
                Views = views,
                Visits = visits
            });
        }
    }

    private async Task LoadRevenueAsync(System.Data.Common.DbConnection conn, DateTime since, DateTime until, string bucketUnit)
    {
        var revenueMap = new Dictionary<DateTime, RevenueBucket>();

        await using (var cmd = (NpgsqlCommand)conn.CreateCommand())
        {
            cmd.CommandTimeout = 10;
            cmd.CommandText = """
                SELECT date_trunc(@unit, ngaybatdau) AS bucket, SUM(sotien) AS total
                FROM dangkyapp
                WHERE ngaybatdau >= @since AND ngaybatdau < @until
                GROUP BY bucket
                ORDER BY bucket
                """;
            cmd.Parameters.Add(new NpgsqlParameter("unit", NpgsqlDbType.Text) { Value = bucketUnit });
            cmd.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = since });
            cmd.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = until });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader.GetDateTime(0);
                var amount = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                revenueMap[bucket] = new RevenueBucket { BucketUtc = bucket, AppRevenue = amount };
            }
        }

        await using (var cmd = (NpgsqlCommand)conn.CreateCommand())
        {
            cmd.CommandTimeout = 10;
            cmd.CommandText = """
                SELECT date_trunc(@unit, ngaythanhtoan) AS bucket, SUM(sotien) AS total
                FROM hoadon
                WHERE ngaythanhtoan >= @since AND ngaythanhtoan < @until
                GROUP BY bucket
                ORDER BY bucket
                """;
            cmd.Parameters.Add(new NpgsqlParameter("unit", NpgsqlDbType.Text) { Value = bucketUnit });
            cmd.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = since });
            cmd.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = until });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var bucket = reader.GetDateTime(0);
                var amount = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                if (!revenueMap.TryGetValue(bucket, out var row))
                {
                    row = new RevenueBucket { BucketUtc = bucket };
                    revenueMap[bucket] = row;
                }
                row.PoiRevenue = amount;
            }
        }

        RevenueBuckets = revenueMap.Values.OrderBy(r => r.BucketUtc).ToList();
    }

    private async Task LoadSummaryAsync(System.Data.Common.DbConnection conn, DateTime since, DateTime until)
    {
        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandTimeout = 8;
        cmd.CommandText = """
            SELECT
                COUNT(DISTINCT mathietbi) FILTER (WHERE mathietbi IS NOT NULL) AS devices,
                SUM(CASE WHEN nguon = 'VIEW' THEN 1 ELSE 0 END) AS views,
                SUM(CASE WHEN upper(nguon) IN ('GPS','APP-GEOFENCE','APP_GEOFENCE','GEOFENCE') THEN 1 ELSE 0 END) AS visits
            FROM lichsuphat
            WHERE thoigian >= @since AND thoigian < @until
            """;
        cmd.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = since });
        cmd.Parameters.Add(new NpgsqlParameter("until", NpgsqlDbType.TimestampTz) { Value = until });

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            TotalActiveDevices = reader.IsDBNull(0) ? 0 : (int)reader.GetInt64(0);
            TotalViews = reader.IsDBNull(1) ? 0 : (int)reader.GetInt64(1);
            TotalVisits = reader.IsDBNull(2) ? 0 : (int)reader.GetInt64(2);
        }

        TotalRevenue = RevenueBuckets.Sum(r => r.AppRevenue + r.PoiRevenue);
    }

    private void FillMissingBuckets(DateTime since, DateTime until, string bucketUnit)
    {
        var activityMap = ActivityBuckets.ToDictionary(b => b.BucketUtc, b => b);
        var revenueMap = RevenueBuckets.ToDictionary(b => b.BucketUtc, b => b);

        var boundaries = BuildBucketBoundaries(since, until, bucketUnit);

        ActivityBuckets = boundaries
            .Select(b => activityMap.TryGetValue(b, out var row)
                ? row
                : new TimeBucket { BucketUtc = b })
            .ToList();

        RevenueBuckets = boundaries
            .Select(b => revenueMap.TryGetValue(b, out var row)
                ? row
                : new RevenueBucket { BucketUtc = b })
            .ToList();
    }

    private static List<DateTime> BuildBucketBoundaries(DateTime since, DateTime until, string bucketUnit)
    {
        var buckets = new List<DateTime>();
        switch (bucketUnit)
        {
            case "hour":
                var hourStart = new DateTime(since.Year, since.Month, since.Day, since.Hour, 0, 0, DateTimeKind.Utc);
                for (var t = hourStart; t < until; t = t.AddHours(1))
                    buckets.Add(t);
                break;
            case "day":
                var dayStart = new DateTime(since.Year, since.Month, since.Day, 0, 0, 0, DateTimeKind.Utc);
                for (var t = dayStart; t < until; t = t.AddDays(1))
                    buckets.Add(t);
                break;
            case "month":
                var monthStart = new DateTime(since.Year, since.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                for (var t = monthStart; t < until; t = t.AddMonths(1))
                    buckets.Add(t);
                break;
        }
        return buckets;
    }

    // ------------------------------------------------------------------
    // Range resolver: mode + optional date/week/month/year/from/to  →  SinceUtc, UntilUtc, Granularity, Label.
    // Rolling presets: "today" | "last7" | "last30" | "last12m".
    // Absolute picks: "day" (needs date), "week" (needs date), "month" (needs month), "year" (needs year).
    // Custom: "custom" (needs from + to).
    // Empty / unknown → defaults to "last7".
    // ------------------------------------------------------------------
    private void ResolveRange(string? mode, string? date, string? week, string? month, int? year, string? from, string? to)
    {
        var now = DateTime.UtcNow;
        var normalized = string.IsNullOrWhiteSpace(mode) ? "last7" : mode.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "today":
                ApplyDay(now.Date);
                Mode = "today";
                RangeLabel = "Hôm nay";
                return;

            case "last7":
                SinceUtc = DateTime.SpecifyKind(now.AddDays(-6).Date, DateTimeKind.Utc);
                UntilUtc = DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc);
                Granularity = "day";
                Mode = "last7";
                RangeLabel = "7 ngày qua";
                return;

            case "last30":
                SinceUtc = DateTime.SpecifyKind(now.AddDays(-29).Date, DateTimeKind.Utc);
                UntilUtc = DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc);
                Granularity = "day";
                Mode = "last30";
                RangeLabel = "30 ngày qua";
                return;

            case "last12m":
                SinceUtc = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-11), DateTimeKind.Utc);
                UntilUtc = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(1), DateTimeKind.Utc);
                Granularity = "month";
                Mode = "last12m";
                RangeLabel = "12 tháng qua";
                return;

            case "day":
                {
                    var picked = TryParseDate(date) ?? now.Date;
                    ApplyDay(picked);
                    Mode = "day";
                    ParamDate = picked.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    RangeLabel = $"Ngày {picked:dd/MM/yyyy}";
                    return;
                }

            case "week":
                {
                    var baseDate = TryParseWeek(week) ?? TryParseDate(date) ?? now.Date;
                    var monday = GetIsoWeekMonday(baseDate);
                    var sunday = monday.AddDays(6);
                    SinceUtc = DateTime.SpecifyKind(monday, DateTimeKind.Utc);
                    UntilUtc = DateTime.SpecifyKind(monday.AddDays(7), DateTimeKind.Utc);
                    Granularity = "day";
                    Mode = "week";
                    ParamWeek = $"{ISOWeek.GetYear(monday):D4}-W{ISOWeek.GetWeekOfYear(monday):D2}";
                    RangeLabel = $"Tuần {monday:dd/MM/yyyy} – {sunday:dd/MM/yyyy}";
                    return;
                }

            case "month":
                {
                    var monthValue = TryParseMonth(month) ?? new DateTime(now.Year, now.Month, 1);
                    SinceUtc = DateTime.SpecifyKind(monthValue, DateTimeKind.Utc);
                    UntilUtc = DateTime.SpecifyKind(monthValue.AddMonths(1), DateTimeKind.Utc);
                    Granularity = "day";
                    Mode = "month";
                    ParamMonth = monthValue.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                    RangeLabel = $"Tháng {monthValue.Month:D2}/{monthValue.Year}";
                    return;
                }

            case "year":
                {
                    var yr = year is > 1999 and < 2200 ? year.Value : now.Year;
                    SinceUtc = DateTime.SpecifyKind(new DateTime(yr, 1, 1), DateTimeKind.Utc);
                    UntilUtc = DateTime.SpecifyKind(new DateTime(yr + 1, 1, 1), DateTimeKind.Utc);
                    Granularity = "month";
                    Mode = "year";
                    ParamYear = yr;
                    RangeLabel = $"Năm {yr}";
                    return;
                }

            case "custom":
                {
                    var f = TryParseDate(from) ?? now.AddDays(-6).Date;
                    var t = TryParseDate(to) ?? now.Date;
                    if (t < f) (f, t) = (t, f);
                    SinceUtc = DateTime.SpecifyKind(f, DateTimeKind.Utc);
                    UntilUtc = DateTime.SpecifyKind(t.AddDays(1), DateTimeKind.Utc);
                    var span = (UntilUtc - SinceUtc).TotalDays;
                    Granularity = span <= 2 ? "hour" : span <= 95 ? "day" : "month";
                    Mode = "custom";
                    ParamFrom = f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    ParamTo = t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    RangeLabel = f == t
                        ? $"Ngày {f:dd/MM/yyyy}"
                        : $"{f:dd/MM/yyyy} – {t:dd/MM/yyyy}";
                    return;
                }

            default:
                SinceUtc = DateTime.SpecifyKind(now.AddDays(-6).Date, DateTimeKind.Utc);
                UntilUtc = DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc);
                Granularity = "day";
                Mode = "last7";
                RangeLabel = "7 ngày qua";
                return;
        }
    }

    private void ApplyDay(DateTime day)
    {
        var start = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
        SinceUtc = start;
        UntilUtc = start.AddDays(1);
        Granularity = "hour";
    }

    private static DateTime? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? d.Date
            : null;
    }

    private static DateTime? TryParseMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? new DateTime(d.Year, d.Month, 1)
            : null;
    }

    private static DateTime? TryParseWeek(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // "2026-W17"
        var parts = value.Split('-');
        if (parts.Length != 2 || !parts[1].StartsWith('W')) return null;
        if (!int.TryParse(parts[0], out var y)) return null;
        if (!int.TryParse(parts[1][1..], out var w)) return null;
        try { return ISOWeek.ToDateTime(y, w, DayOfWeek.Monday); } catch { return null; }
    }

    private static DateTime GetIsoWeekMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff).Date;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOpts);

    private void ResetAnalytics()
    {
        ActivityBuckets = [];
        GeoPoints = [];
        TopPoi = [];
        RevenueBuckets = [];
        TotalActiveDevices = 0;
        TotalVisits = 0;
        TotalViews = 0;
        TotalRevenue = 0m;
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
        try { NpgsqlConnection.ClearAllPools(); } catch { }
    }

    public sealed class TimeBucket
    {
        public DateTime BucketUtc { get; set; }
        public int ActiveUsers { get; set; }
        public int Views { get; set; }
        public int Visits { get; set; }
    }

    public sealed class GeoHeatPoint
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Name { get; set; } = "";
        public int Weight { get; set; }
    }

    public sealed class PoiStat
    {
        public Guid PoiId { get; set; }
        public string Name { get; set; } = "";
        public int Views { get; set; }
        public int Visits { get; set; }
        public int Total => Views + Visits;
    }

    public sealed class RevenueBucket
    {
        public DateTime BucketUtc { get; set; }
        public decimal AppRevenue { get; set; }
        public decimal PoiRevenue { get; set; }
        public decimal Total => AppRevenue + PoiRevenue;
    }
}
