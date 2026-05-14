using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.API.Tests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Health_ReturnsOkStatus()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubscriptionTrial_CanBeUsedOnlyOnce_AndStatusReflectsActivePlan()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var firstPurchase = await client.PostAsJsonAsync(
            "/api/subscription/purchase",
            new { MaThietBi = " device-trial-01 ", LoaiGoi = "thu" });

        Assert.Equal(HttpStatusCode.OK, firstPurchase.StatusCode);
        var firstBody = await ReadJsonAsync(firstPurchase);
        Assert.True(Property(firstBody.RootElement, "MienPhi").GetBoolean());
        Assert.Equal(3, Property(firstBody.RootElement, "SoNgayConLai").GetInt32());

        var status = await client.GetFromJsonAsync<JsonDocument>("/api/subscription/status/device-trial-01");
        Assert.NotNull(status);
        Assert.True(Property(status!.RootElement, "CoDangKy").GetBoolean());
        Assert.True(Property(status.RootElement, "DaDungThu").GetBoolean());
        Assert.Equal("thu", Property(status.RootElement, "LoaiGoi").GetString());

        var secondPurchase = await client.PostAsJsonAsync(
            "/api/subscription/purchase",
            new { MaThietBi = "device-trial-01", LoaiGoi = "thu" });

        Assert.Equal(HttpStatusCode.BadRequest, secondPurchase.StatusCode);
    }

    [Fact]
    public async Task SubscriptionRequest_Approve_ActivatesPaidPlanAndPreventsDuplicateApproval()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/subscription/request",
            new { MaThietBi = "device-paid-01", LoaiGoi = "tuan" });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createBody = await ReadJsonAsync(createResponse);
        var requestId = Property(createBody.RootElement, "YeuCauId").GetGuid();
        Assert.Equal(99_000m, Property(createBody.RootElement, "SoTien").GetDecimal());
        Assert.Equal("cho_duyet", Property(createBody.RootElement, "TrangThai").GetString());
        Assert.StartsWith("VKT TUAN DEVICE", Property(createBody.RootElement, "NoiDungChuyen").GetString());

        using var adminClient = CreateAdminClient(factory);
        var approveResponse = await adminClient.PostAsJsonAsync(
            $"/api/subscription/approve/{requestId}",
            new { GhiChu = "Da doi soat" });

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var requestStatus = await client.GetFromJsonAsync<JsonDocument>($"/api/subscription/request/{requestId}");
        Assert.NotNull(requestStatus);
        Assert.Equal("da_duyet", Property(requestStatus!.RootElement, "TrangThai").GetString());
        Assert.False(Property(requestStatus.RootElement, "NgayHetHan").ValueKind == JsonValueKind.Null);

        var subscriptionStatus = await client.GetFromJsonAsync<JsonDocument>("/api/subscription/status/device-paid-01");
        Assert.NotNull(subscriptionStatus);
        Assert.True(Property(subscriptionStatus!.RootElement, "CoDangKy").GetBoolean());
        Assert.Equal("tuan", Property(subscriptionStatus.RootElement, "LoaiGoi").GetString());

        var duplicateApproval = await adminClient.PostAsJsonAsync(
            $"/api/subscription/approve/{requestId}",
            new { GhiChu = "Approve again" });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateApproval.StatusCode);
    }

    [Fact]
    public async Task SubscriptionRequest_ApproveAndReject_RequireAdminToken()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var createApproveResponse = await client.PostAsJsonAsync(
            "/api/subscription/request",
            new { MaThietBi = "device-auth-approve", LoaiGoi = "tuan" });
        Assert.Equal(HttpStatusCode.OK, createApproveResponse.StatusCode);
        var createApproveBody = await ReadJsonAsync(createApproveResponse);
        var approveRequestId = Property(createApproveBody.RootElement, "YeuCauId").GetGuid();

        var createRejectResponse = await client.PostAsJsonAsync(
            "/api/subscription/request",
            new { MaThietBi = "device-auth-reject", LoaiGoi = "ngay" });
        Assert.Equal(HttpStatusCode.OK, createRejectResponse.StatusCode);
        var createRejectBody = await ReadJsonAsync(createRejectResponse);
        var rejectRequestId = Property(createRejectBody.RootElement, "YeuCauId").GetGuid();

        var approveWithoutToken = await client.PostAsJsonAsync(
            $"/api/subscription/approve/{approveRequestId}",
            new { GhiChu = "No token" });
        var rejectWithoutToken = await client.PostAsJsonAsync(
            $"/api/subscription/reject/{rejectRequestId}",
            new { GhiChu = "No token" });

        Assert.Equal(HttpStatusCode.Unauthorized, approveWithoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectWithoutToken.StatusCode);

        await factory.AssertAsync(async db =>
        {
            var approveRequest = await db.YeuCauThanhToans.SingleAsync(y => y.Id == approveRequestId);
            var rejectRequest = await db.YeuCauThanhToans.SingleAsync(y => y.Id == rejectRequestId);

            Assert.Equal("cho_duyet", approveRequest.TrangThai);
            Assert.Equal("cho_duyet", rejectRequest.TrangThai);
            Assert.False(await db.DangKyApps.AnyAsync(d => d.MaThietBi == "device-auth-approve"));
        });
    }

    [Fact]
    public async Task SubscriptionPurchase_RejectsInvalidInput_AndExtendsPaidPlanFromCurrentExpiry()
    {
        using var factory = new ApiTestApplicationFactory();
        var currentExpiry = DateTime.UtcNow.AddDays(5);
        await factory.SeedAsync(db =>
        {
            db.DangKyApps.Add(new DangKyApp
            {
                Id = Guid.NewGuid(),
                MaThietBi = "device-renew-01",
                LoaiGoi = "ngay",
                NgayBatDau = DateTime.UtcNow.AddDays(-1),
                NgayHetHan = currentExpiry,
                SoTien = 29_000m
            });

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var missingDevice = await client.PostAsJsonAsync(
            "/api/subscription/purchase",
            new { MaThietBi = "", LoaiGoi = "ngay" });
        Assert.Equal(HttpStatusCode.BadRequest, missingDevice.StatusCode);

        var invalidPlan = await client.PostAsJsonAsync(
            "/api/subscription/purchase",
            new { MaThietBi = "device-renew-01", LoaiGoi = "khong-co-goi" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPlan.StatusCode);

        var renew = await client.PostAsJsonAsync(
            "/api/subscription/purchase",
            new { MaThietBi = "device-renew-01", LoaiGoi = "ngay" });
        Assert.Equal(HttpStatusCode.OK, renew.StatusCode);

        await factory.AssertAsync(async db =>
        {
            var renewed = await db.DangKyApps
                .Where(d => d.MaThietBi == "device-renew-01")
                .OrderByDescending(d => d.NgayHetHan)
                .FirstAsync();

            Assert.InRange((renewed.NgayHetHan - currentExpiry.AddDays(1)).TotalSeconds, -5, 5);
        });
    }

    [Fact]
    public async Task SubscriptionRequest_RejectsTrialPlan_AndRejectsPendingRequest()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var trialRequest = await client.PostAsJsonAsync(
            "/api/subscription/request",
            new { MaThietBi = "device-reject-01", LoaiGoi = "thu" });
        Assert.Equal(HttpStatusCode.BadRequest, trialRequest.StatusCode);

        var createResponse = await client.PostAsJsonAsync(
            "/api/subscription/request",
            new { MaThietBi = "device-reject-01", LoaiGoi = "ngay" });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createBody = await ReadJsonAsync(createResponse);
        var requestId = Property(createBody.RootElement, "YeuCauId").GetGuid();

        using var adminClient = CreateAdminClient(factory);
        var rejectResponse = await adminClient.PostAsJsonAsync(
            $"/api/subscription/reject/{requestId}",
            new { GhiChu = "Sai noi dung chuyen khoan" });
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var requestStatus = await client.GetFromJsonAsync<JsonDocument>($"/api/subscription/request/{requestId}");
        Assert.NotNull(requestStatus);
        Assert.Equal("tu_choi", Property(requestStatus!.RootElement, "TrangThai").GetString());
        Assert.Equal("Sai noi dung chuyen khoan", Property(requestStatus.RootElement, "GhiChuAdmin").GetString());

        var duplicateReject = await adminClient.PostAsJsonAsync(
            $"/api/subscription/reject/{requestId}",
            new { GhiChu = "Reject again" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicateReject.StatusCode);
    }

    [Fact]
    public async Task PoiList_ReturnsOnlyActiveMaintainedPois_InPriorityOrder()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.SeedAsync(db =>
        {
            db.POIs.AddRange(
                CreatePoi("Hidden expired", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(-1), active: true),
                CreatePoi("Visible second", priority: 2, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: true),
                CreatePoi("Hidden inactive", priority: 3, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: false),
                CreatePoi("Visible first", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: true));

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var pois = await client.GetFromJsonAsync<JsonElement[]>("/api/poi");

        Assert.NotNull(pois);
        Assert.Equal(["Visible first", "Visible second"], pois!.Select(p => Property(p, "TenPOI").GetString()));
    }

    [Fact]
    public async Task PoiDetail_UsesRequestedTranslation_AndHidesInactiveMenuItems()
    {
        using var factory = new ApiTestApplicationFactory();
        var poiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            var poi = CreatePoi("Quan nghe song", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: true);
            poi.Id = poiId;
            poi.ThuyetMinhs.Add(new ThuyetMinh
            {
                Id = Guid.NewGuid(),
                POIId = poiId,
                TrangThai = true,
                BanDichs =
                [
                    new BanDich { Id = Guid.NewGuid(), NgonNgu = "vi", NoiDung = "Noi dung tieng Viet", FileAudio = "vi.mp3" },
                    new BanDich { Id = Guid.NewGuid(), NgonNgu = "en", NoiDung = "English narration", FileAudio = "en.mp3" }
                ]
            });
            poi.MonAns.Add(new MonAn { Id = Guid.NewGuid(), POIId = poiId, TenMonAn = "Ca kho", DonGia = 80_000, TinhTrang = true });
            poi.MonAns.Add(new MonAn { Id = Guid.NewGuid(), POIId = poiId, TenMonAn = "Mon tam ngung", DonGia = 10_000, TinhTrang = false });
            db.POIs.Add(poi);

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<JsonDocument>($"/api/poi/{poiId}?lang=en");

        Assert.NotNull(detail);
        Assert.Equal("English narration", Property(detail!.RootElement, "NoiDungThuyetMinh").GetString());
        Assert.Equal("en.mp3", Property(detail.RootElement, "FileAudio").GetString());
        var menuNames = Property(detail.RootElement, "MonAns")
            .EnumerateArray()
            .Select(m => Property(m, "TenMonAn").GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(new[] { "Ca kho" }, menuNames);
    }

    [Fact]
    public async Task PoiDetail_FallsBackToVietnameseTranslation_WhenRequestedLanguageIsMissing()
    {
        using var factory = new ApiTestApplicationFactory();
        var poiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            var poi = CreatePoi("Quan fallback", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: true, id: poiId);
            poi.ThuyetMinhs.Add(new ThuyetMinh
            {
                Id = Guid.NewGuid(),
                POIId = poiId,
                TrangThai = true,
                BanDichs =
                [
                    new BanDich
                    {
                        Id = Guid.NewGuid(),
                        NgonNgu = "vi",
                        NoiDung = "Ban tieng Viet mac dinh",
                        FileAudio = "fallback-vi.mp3"
                    }
                ]
            });
            db.POIs.Add(poi);

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var detail = await client.GetFromJsonAsync<JsonDocument>($"/api/poi/{poiId}?lang=fr");

        Assert.NotNull(detail);
        Assert.Equal("Ban tieng Viet mac dinh", Property(detail!.RootElement, "NoiDungThuyetMinh").GetString());
        Assert.Equal("fallback-vi.mp3", Property(detail.RootElement, "FileAudio").GetString());
    }

    [Fact]
    public async Task MaintenancePayment_ExtendsFromExistingExpiry_AndCreatesMonthlyInvoices()
    {
        using var factory = new ApiTestApplicationFactory();
        var poiId = Guid.NewGuid();
        var currentExpiry = DateTime.UtcNow.AddDays(10);
        await factory.SeedAsync(db =>
        {
            db.POIs.Add(CreatePoi("Quan can gia han", priority: 1, expiresAtUtc: currentExpiry, active: true, id: poiId));
            db.DangKyDichVus.Add(new DangKyDichVu
            {
                Id = Guid.NewGuid(),
                POIId = poiId,
                PhiDuyTriThang = 75_000m,
                PhiConvert = 25_000m,
                NgayBatDau = DateTime.UtcNow.AddMonths(-1),
                NgayHetHan = currentExpiry,
                TrangThai = true
            });

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/payment/maintenance",
            new { PoiId = poiId, TaiKhoanId = (Guid?)null, SoThangGiaHan = 2, GhiChu = "Thu tien mat" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(150_000m, Property(body.RootElement, "TongTien").GetDecimal());

        await factory.AssertAsync(async db =>
        {
            var poi = await db.POIs.SingleAsync(p => p.Id == poiId);
            Assert.True(poi.TrangThai);
            Assert.NotNull(poi.NgayHetHanDuyTri);
            Assert.InRange((poi.NgayHetHanDuyTri.Value - currentExpiry.AddMonths(2)).TotalSeconds, -5, 5);

            var invoices = await db.HoaDons
                .Where(h => h.POIId == poiId && h.LoaiPhi == "duytri")
                .OrderBy(h => h.KyThanhToan)
                .ToListAsync();

            Assert.Equal(2, invoices.Count);
            Assert.All(invoices, invoice => Assert.Equal(75_000m, invoice.SoTien));
        });
    }

    [Fact]
    public async Task MaintenancePayment_RejectsInvalidMonthCount_AndMissingPoi()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var invalidMonthCount = await client.PostAsJsonAsync(
            "/api/payment/maintenance",
            new { PoiId = Guid.NewGuid(), TaiKhoanId = (Guid?)null, SoThangGiaHan = 0, GhiChu = "Invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidMonthCount.StatusCode);

        var missingPoi = await client.PostAsJsonAsync(
            "/api/payment/maintenance",
            new { PoiId = Guid.NewGuid(), TaiKhoanId = (Guid?)null, SoThangGiaHan = 1, GhiChu = "Missing" });
        Assert.Equal(HttpStatusCode.NotFound, missingPoi.StatusCode);
    }

    [Fact]
    public async Task ConvertPayment_RequiresMaintainedPoi_AndCreatesInvoiceWithConfiguredFee()
    {
        using var factory = new ApiTestApplicationFactory();
        var expiredPoiId = Guid.NewGuid();
        var activePoiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.POIs.AddRange(
                CreatePoi("Quan het han", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(-1), active: true, id: expiredPoiId),
                CreatePoi("Quan con han", priority: 2, expiresAtUtc: DateTime.UtcNow.AddDays(15), active: true, id: activePoiId));
            db.DangKyDichVus.Add(new DangKyDichVu
            {
                Id = Guid.NewGuid(),
                POIId = activePoiId,
                PhiDuyTriThang = 50_000m,
                PhiConvert = 35_000m,
                NgayBatDau = DateTime.UtcNow.AddMonths(-1),
                NgayHetHan = DateTime.UtcNow.AddDays(15),
                TrangThai = true
            });

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var missingPoi = await client.PostAsJsonAsync(
            $"/api/payment/convert/{Guid.NewGuid()}",
            new { TaiKhoanId = (Guid?)null, GhiChu = "Missing" });
        Assert.Equal(HttpStatusCode.NotFound, missingPoi.StatusCode);

        var expiredPoi = await client.PostAsJsonAsync(
            $"/api/payment/convert/{expiredPoiId}",
            new { TaiKhoanId = (Guid?)null, GhiChu = "Expired" });
        Assert.Equal(HttpStatusCode.BadRequest, expiredPoi.StatusCode);

        var activePoi = await client.PostAsJsonAsync(
            $"/api/payment/convert/{activePoiId}",
            new { TaiKhoanId = (Guid?)null, GhiChu = "Convert now" });
        Assert.Equal(HttpStatusCode.OK, activePoi.StatusCode);
        var activeBody = await ReadJsonAsync(activePoi);
        Assert.True(Property(activeBody.RootElement, "CanConvert").GetBoolean());
        Assert.Equal(35_000m, Property(activeBody.RootElement, "SoTien").GetDecimal());

        await factory.AssertAsync(async db =>
        {
            var invoice = await db.HoaDons.SingleAsync(h => h.POIId == activePoiId && h.LoaiPhi == "convert");

            Assert.Equal(35_000m, invoice.SoTien);
            Assert.Null(invoice.KyThanhToan);
            Assert.Equal("Convert now", invoice.GhiChu);
        });
    }

    private static POI CreatePoi(
        string name,
        int priority,
        DateTime? expiresAtUtc,
        bool active,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenPOI = name,
            KinhDo = 106.7,
            ViDo = 10.8,
            BanKinh = 40,
            MucUuTien = priority,
            TrangThai = active,
            NgayHetHanDuyTri = expiresAtUtc,
            DiaChi = "Vinh Khanh",
            SDT = "0900000000"
        };

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static JsonElement Property(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value))
            return value;

        var camelName = char.ToLowerInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(camelName, out value))
            return value;

        throw new KeyNotFoundException($"JSON property '{name}' was not found.");
    }

    private static HttpClient CreateAdminClient(ApiTestApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Token", ApiTestApplicationFactory.AdminApiToken);
        return client;
    }
}

public sealed class ApiTestApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminApiToken = "integration-test-admin-token";

    private readonly bool _usePostgres = string.Equals(
        Environment.GetEnvironmentVariable("API_TEST_DATABASE"),
        "postgres",
        StringComparison.OrdinalIgnoreCase);
    private readonly SqliteConnection? _sqliteConnection;
    private readonly string? _postgresAdminConnectionString;
    private readonly string? _postgresConnectionString;
    private readonly string? _postgresDatabaseName;

    public ApiTestApplicationFactory()
    {
        if (_usePostgres)
        {
            var baseConnectionString = Environment.GetEnvironmentVariable("API_TEST_CONNECTION_STRING")
                ?? Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
                ?? throw new InvalidOperationException(
                    "Set API_TEST_CONNECTION_STRING when API_TEST_DATABASE=postgres.");

            _postgresDatabaseName = $"vinhkhanhtour_api_tests_{Guid.NewGuid():N}";

            var testBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = _postgresDatabaseName,
                Pooling = false
            };
            _postgresConnectionString = testBuilder.ConnectionString;

            var adminBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = "postgres",
                Pooling = false
            };
            _postgresAdminConnectionString = adminBuilder.ConnectionString;
            return;
        }

        _sqliteConnection = new SqliteConnection("Data Source=:memory:");
        _sqliteConnection.Open();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await seed(db);
    }

    public async Task AssertAsync(Func<AppDbContext, Task> assert)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await assert(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    _postgresConnectionString
                    ?? "Host=localhost;Port=5432;Database=vinhkhanhtour_test;Username=postgres;Password=postgres;Timeout=1;Command Timeout=1;Pooling=false",
                ["Database:DisableSslForLocalDev"] = "true",
                ["AdminApi:Token"] = AdminApiToken
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);

            if (_usePostgres)
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(_postgresConnectionString));
                return;
            }

            var sqliteProvider = new ServiceCollection()
                .AddEntityFrameworkSqlite()
                .BuildServiceProvider();
            var sqliteConnection = _sqliteConnection
                ?? throw new InvalidOperationException("SQLite connection was not initialized.");

            services.AddDbContext<AppDbContext>(options => options
                .UseSqlite(sqliteConnection)
                .UseInternalServiceProvider(sqliteProvider));
        });
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions>();
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();

        for (var index = services.Count - 1; index >= 0; index--)
        {
            var serviceTypeName = services[index].ServiceType.FullName ?? services[index].ServiceType.Name;
            if (serviceTypeName.Contains("DbContextOptionsConfiguration", StringComparison.Ordinal))
                services.RemoveAt(index);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _sqliteConnection?.Dispose();

        if (!_usePostgres
            || string.IsNullOrWhiteSpace(_postgresAdminConnectionString)
            || string.IsNullOrWhiteSpace(_postgresDatabaseName))
        {
            return;
        }

        try
        {
            NpgsqlConnection.ClearAllPools();
            using var connection = new NpgsqlConnection(_postgresAdminConnectionString);
            connection.Open();

            using var terminate = connection.CreateCommand();
            terminate.CommandText = """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @databaseName AND pid <> pg_backend_pid();
                """;
            terminate.Parameters.AddWithValue("databaseName", _postgresDatabaseName);
            terminate.ExecuteNonQuery();

            using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS {QuoteIdentifier(_postgresDatabaseName)}";
            drop.ExecuteNonQuery();
        }
        catch
        {
            // Test database cleanup is best-effort; CI Postgres services are ephemeral.
        }
    }

    private static string QuoteIdentifier(string identifier)
        => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
