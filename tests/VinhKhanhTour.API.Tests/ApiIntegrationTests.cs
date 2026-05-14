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
    public async Task CmsCreatedPoi_IsVisibleToAppPoiListAndDetailApi()
    {
        using var factory = new ApiTestApplicationFactory();
        var poiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            var poi = CreatePoi("CMS Created App Visible", priority: 3, expiresAtUtc: DateTime.UtcNow.AddMonths(1), active: true, id: poiId);
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
                        NoiDung = "Noi dung do admin CMS nhap",
                        FileAudio = "cms-created.mp3"
                    }
                ]
            });
            poi.MonAns.Add(new MonAn
            {
                Id = Guid.NewGuid(),
                POIId = poiId,
                TenMonAn = "Mon CMS tao",
                DonGia = 45_000m,
                TinhTrang = true
            });
            db.POIs.Add(poi);

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var pois = await client.GetFromJsonAsync<JsonElement[]>("/api/poi");
        var detail = await client.GetFromJsonAsync<JsonDocument>($"/api/poi/{poiId}?lang=vi");

        Assert.NotNull(pois);
        Assert.Contains(pois!, poi => Property(poi, "Id").GetGuid() == poiId);
        Assert.NotNull(detail);
        Assert.Equal("CMS Created App Visible", Property(detail!.RootElement, "TenPOI").GetString());
        Assert.Equal("Noi dung do admin CMS nhap", Property(detail.RootElement, "NoiDungThuyetMinh").GetString());
        Assert.Equal("cms-created.mp3", Property(detail.RootElement, "FileAudio").GetString());
        var menuNames = Property(detail.RootElement, "MonAns")
            .EnumerateArray()
            .Select(m => Property(m, "TenMonAn").GetString())
            .ToArray();
        Assert.Contains("Mon CMS tao", menuNames);
    }

    [Fact]
    public async Task ExpiredPoi_BecomesVisibleToAppApiAfterCmsMaintenancePayment()
    {
        using var factory = new ApiTestApplicationFactory();
        var poiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.POIs.Add(CreatePoi("Expired Then Renewed", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(-3), active: true, id: poiId));
            db.DangKyDichVus.Add(new DangKyDichVu
            {
                Id = Guid.NewGuid(),
                POIId = poiId,
                PhiDuyTriThang = 60_000m,
                PhiConvert = 20_000m,
                NgayBatDau = DateTime.UtcNow.AddMonths(-2),
                NgayHetHan = DateTime.UtcNow.AddDays(-3),
                TrangThai = true
            });

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();
        using var adminClient = CreateAdminClient(factory);

        var beforeRenewal = await client.GetFromJsonAsync<JsonElement[]>("/api/poi");
        Assert.DoesNotContain(beforeRenewal ?? [], poi => Property(poi, "Id").GetGuid() == poiId);

        var renewal = await adminClient.PostAsJsonAsync(
            "/api/payment/maintenance",
            new { PoiId = poiId, TaiKhoanId = (Guid?)null, SoThangGiaHan = 1, GhiChu = "CMS ghi nhan gia han" });
        Assert.Equal(HttpStatusCode.OK, renewal.StatusCode);

        var afterRenewal = await client.GetFromJsonAsync<JsonElement[]>("/api/poi");
        Assert.Contains(afterRenewal ?? [], poi => Property(poi, "Id").GetGuid() == poiId);

        await factory.AssertAsync(async db =>
        {
            var poi = await db.POIs.SingleAsync(p => p.Id == poiId);
            Assert.True(poi.TrangThai);
            Assert.True(poi.NgayHetHanDuyTri > DateTime.UtcNow);
            Assert.True(await db.HoaDons.AnyAsync(h => h.POIId == poiId && h.LoaiPhi == "duytri"));
        });
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
        using var adminClient = CreateAdminClient(factory);

        var response = await adminClient.PostAsJsonAsync(
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
        using var adminClient = CreateAdminClient(factory);

        var withoutToken = await client.PostAsJsonAsync(
            "/api/payment/maintenance",
            new { PoiId = Guid.NewGuid(), TaiKhoanId = (Guid?)null, SoThangGiaHan = 1, GhiChu = "No token" });
        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.StatusCode);

        var invalidMonthCount = await adminClient.PostAsJsonAsync(
            "/api/payment/maintenance",
            new { PoiId = Guid.NewGuid(), TaiKhoanId = (Guid?)null, SoThangGiaHan = 0, GhiChu = "Invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidMonthCount.StatusCode);

        var missingPoi = await adminClient.PostAsJsonAsync(
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

    [Fact]
    public async Task Auth_RegisterAndLogin_RejectInvalidDuplicateAndInactiveUsers()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var weakPassword = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { TenDangNhap = "guest01", MatKhau = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                TenDangNhap = "guest01",
                MatKhau = "secret1",
                TenTaiKhoan = "Guest One",
                Email = "guest01@example.test",
                SoDienThoai = "0909000001"
            });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var registerBody = await ReadJsonAsync(register);
        Assert.Equal("guest01", Property(registerBody.RootElement, "TenDangNhap").GetString());
        Assert.Equal("khach", Property(registerBody.RootElement, "VaiTro").GetString());

        var duplicate = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { TenDangNhap = "guest01", MatKhau = "secret1" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { TenDangNhap = "guest01", MatKhau = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { TenDangNhap = "guest01", MatKhau = "secret1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        await factory.AssertAsync(async db =>
        {
            var user = await db.TaiKhoans.SingleAsync(t => t.TenDangNhap == "guest01");
            user.TrangThai = false;
            await db.SaveChangesAsync();
        });

        var inactiveLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { TenDangNhap = "guest01", MatKhau = "secret1" });
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);
    }

    [Fact]
    public async Task Upload_AcceptsImageAndRejectsInvalidEmptyOrLargeFiles()
    {
        using var factory = new ApiTestApplicationFactory();
        using var client = factory.CreateClient();

        using var missingFileContent = new MultipartFormDataContent();
        var missingFile = await client.PostAsync("/api/upload", missingFileContent);
        Assert.Equal(HttpStatusCode.BadRequest, missingFile.StatusCode);

        using var invalidFileContent = new MultipartFormDataContent();
        invalidFileContent.Add(new ByteArrayContent("not-an-image"u8.ToArray()), "file", "note.txt");
        var invalidFile = await client.PostAsync("/api/upload", invalidFileContent);
        Assert.Equal(HttpStatusCode.BadRequest, invalidFile.StatusCode);

        using var largeFileContent = new MultipartFormDataContent();
        largeFileContent.Add(new ByteArrayContent(new byte[(5 * 1024 * 1024) + 1]), "file", "big.png");
        var largeFile = await client.PostAsync("/api/upload", largeFileContent);
        Assert.Equal(HttpStatusCode.BadRequest, largeFile.StatusCode);

        using var imageContent = new MultipartFormDataContent();
        imageContent.Add(new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A]), "file", "cover.png");
        var imageUpload = await client.PostAsync("/api/upload", imageContent);
        Assert.Equal(HttpStatusCode.OK, imageUpload.StatusCode);
        var uploadBody = await ReadJsonAsync(imageUpload);
        var url = Property(uploadBody.RootElement, "url").GetString();
        Assert.NotNull(url);
        Assert.StartsWith("/uploads/", url);
        Assert.EndsWith(".png", url);

        var uploadedFile = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, uploadedFile.StatusCode);
    }

    [Fact]
    public async Task ThuyetMinhEndpoint_ReturnsRequestedLanguageFallbackAndVisibilityRules()
    {
        using var factory = new ApiTestApplicationFactory();
        var visiblePoiId = Guid.NewGuid();
        var expiredPoiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            var visiblePoi = CreatePoi("Narration visible", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(10), active: true, id: visiblePoiId);
            visiblePoi.ThuyetMinhs.Add(new ThuyetMinh
            {
                Id = Guid.NewGuid(),
                POIId = visiblePoiId,
                TrangThai = true,
                ThuTu = 1,
                BanDichs =
                [
                    new BanDich { Id = Guid.NewGuid(), NgonNgu = "vi", NoiDung = "Noi dung mac dinh", FileAudio = "vi.mp3" },
                    new BanDich { Id = Guid.NewGuid(), NgonNgu = "en", NoiDung = "English guide", FileAudio = "en.mp3" }
                ]
            });

            db.POIs.AddRange(
                visiblePoi,
                CreatePoi("Narration expired", priority: 2, expiresAtUtc: DateTime.UtcNow.AddDays(-1), active: true, id: expiredPoiId));

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var english = await client.GetFromJsonAsync<JsonDocument>($"/api/thuyet-minh/{visiblePoiId}?lang=en-US");
        Assert.NotNull(english);
        Assert.Equal("English guide", Property(english!.RootElement, "NoiDung").GetString());
        Assert.Equal("en", Property(english.RootElement, "NgonNgu").GetString());

        var fallback = await client.GetFromJsonAsync<JsonDocument>($"/api/thuyet-minh/{visiblePoiId}?lang=fr");
        Assert.NotNull(fallback);
        Assert.Equal("Noi dung mac dinh", Property(fallback!.RootElement, "NoiDung").GetString());
        Assert.Equal("vi", Property(fallback.RootElement, "NgonNgu").GetString());

        var expired = await client.GetAsync($"/api/thuyet-minh/{expiredPoiId}?lang=vi");
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
    }

    [Fact]
    public async Task PaymentStatusHistoryAndOverdue_ReturnExpectedPoiPaymentData()
    {
        using var factory = new ApiTestApplicationFactory();
        var activePoiId = Guid.NewGuid();
        var overduePoiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.POIs.AddRange(
                CreatePoi("Payment active", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(8), active: true, id: activePoiId),
                CreatePoi("Payment overdue", priority: 2, expiresAtUtc: DateTime.UtcNow.AddDays(-4), active: true, id: overduePoiId));
            db.DangKyDichVus.Add(new DangKyDichVu
            {
                Id = Guid.NewGuid(),
                POIId = activePoiId,
                PhiDuyTriThang = 88_000m,
                PhiConvert = 33_000m,
                NgayBatDau = DateTime.UtcNow.AddMonths(-1),
                NgayHetHan = DateTime.UtcNow.AddDays(8),
                TrangThai = true
            });
            db.HoaDons.AddRange(
                new HoaDon { Id = Guid.NewGuid(), POIId = activePoiId, LoaiPhi = "duytri", SoTien = 88_000m, KyThanhToan = "2026-05", GhiChu = "paid" },
                new HoaDon { Id = Guid.NewGuid(), POIId = activePoiId, LoaiPhi = "convert", SoTien = 33_000m, GhiChu = "tts" });

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<JsonDocument>($"/api/payment/status/{activePoiId}");
        Assert.NotNull(status);
        Assert.Equal("Payment active", Property(status!.RootElement, "TenPOI").GetString());
        Assert.False(Property(status.RootElement, "HetHanDuyTri").GetBoolean());
        Assert.Equal(88_000m, Property(status.RootElement, "PhiDuyTriThang").GetDecimal());
        Assert.Equal(33_000m, Property(status.RootElement, "PhiConvert").GetDecimal());

        var history = await client.GetFromJsonAsync<JsonElement[]>($"/api/payment/history/{activePoiId}");
        Assert.NotNull(history);
        Assert.Equal(2, history!.Length);
        Assert.Contains(history, invoice => Property(invoice, "LoaiPhi").GetString() == "convert");

        var overdue = await client.GetFromJsonAsync<JsonElement[]>("/api/payment/overdue");
        Assert.NotNull(overdue);
        Assert.Contains(overdue!, poi => Property(poi, "Id").GetGuid() == overduePoiId);
        Assert.DoesNotContain(overdue!, poi => Property(poi, "Id").GetGuid() == activePoiId);

        var missingStatus = await client.GetAsync($"/api/payment/status/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingStatus.StatusCode);
    }

    [Fact]
    public async Task SubscriptionPlansRequestsAndRequestStatus_FilterAndExposePaymentState()
    {
        using var factory = new ApiTestApplicationFactory();
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var plans = await client.GetFromJsonAsync<JsonElement[]>("/api/subscription/plans");
        Assert.NotNull(plans);
        Assert.Contains(plans!, plan => Property(plan, "LoaiGoi").GetString() == "thu" && Property(plan, "MienPhi").GetBoolean());
        Assert.Contains(plans!, plan => Property(plan, "LoaiGoi").GetString() == "nam" && Property(plan, "Gia").GetDecimal() == 999_000m);

        var createPending = await client.PostAsJsonAsync(
            "/api/subscription/request",
            new { MaThietBi = "device-filter-pending", LoaiGoi = "thang" });
        Assert.Equal(HttpStatusCode.OK, createPending.StatusCode);
        var pendingBody = await ReadJsonAsync(createPending);
        var pendingId = Property(pendingBody.RootElement, "YeuCauId").GetGuid();

        var createRejected = await client.PostAsJsonAsync(
            "/api/subscription/request",
            new { MaThietBi = "device-filter-rejected", LoaiGoi = "ngay" });
        Assert.Equal(HttpStatusCode.OK, createRejected.StatusCode);
        var rejectedBody = await ReadJsonAsync(createRejected);
        var rejectedId = Property(rejectedBody.RootElement, "YeuCauId").GetGuid();

        using var adminClient = CreateAdminClient(factory);
        var reject = await adminClient.PostAsJsonAsync(
            $"/api/subscription/reject/{rejectedId}",
            new { GhiChu = "Khong tim thay giao dich" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var allRequests = await client.GetFromJsonAsync<JsonElement[]>("/api/subscription/requests");
        Assert.NotNull(allRequests);
        Assert.Equal(2, allRequests!.Length);

        var pendingRequests = await client.GetFromJsonAsync<JsonElement[]>("/api/subscription/requests?trangthai=cho_duyet");
        Assert.NotNull(pendingRequests);
        Assert.Single(pendingRequests!);
        Assert.Equal(pendingId, Property(pendingRequests![0], "Id").GetGuid());

        var rejectedStatus = await client.GetFromJsonAsync<JsonDocument>($"/api/subscription/request/{rejectedId}");
        Assert.NotNull(rejectedStatus);
        Assert.Equal("tu_choi", Property(rejectedStatus!.RootElement, "TrangThai").GetString());
        Assert.Equal("Khong tim thay giao dich", Property(rejectedStatus.RootElement, "GhiChuAdmin").GetString());

        var missing = await client.GetAsync($"/api/subscription/request/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_TracksVerifiedPoiVisitsViewsProfileActiveAndHistory()
    {
        using var factory = new ApiTestApplicationFactory();
        var activePoiId = Guid.NewGuid();
        var farPoiId = Guid.NewGuid();
        var secondPoiId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            var activePoi = CreatePoi("Heartbeat Active POI", priority: 1, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: true, id: activePoiId);
            activePoi.ViDo = 10.7589;
            activePoi.KinhDo = 106.7018;
            activePoi.BanKinh = 50;

            var farPoi = CreatePoi("Heartbeat Far Claim", priority: 2, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: true, id: farPoiId);
            farPoi.ViDo = 10.8000;
            farPoi.KinhDo = 106.8000;
            farPoi.BanKinh = 10;

            var secondPoi = CreatePoi("Heartbeat Second POI", priority: 3, expiresAtUtc: DateTime.UtcNow.AddDays(30), active: true, id: secondPoiId);
            secondPoi.ViDo = 10.7592;
            secondPoi.KinhDo = 106.7020;
            secondPoi.BanKinh = 50;

            db.POIs.AddRange(
                activePoi,
                farPoi,
                secondPoi);
            db.DangKyApps.Add(new DangKyApp
            {
                Id = Guid.NewGuid(),
                MaThietBi = "heartbeat-device-01",
                LoaiGoi = "thang",
                NgayBatDau = DateTime.UtcNow.AddDays(-1),
                NgayHetHan = DateTime.UtcNow.AddDays(20),
                SoTien = 199_000m
            });

            return db.SaveChangesAsync();
        });

        using var client = factory.CreateClient();

        var outsideArea = await client.PostAsJsonAsync(
            "/api/heartbeat",
            new { MaThietBi = "heartbeat-device-01", Lat = 0, Lng = 0, PoiIdHienTai = activePoiId });
        Assert.Equal(HttpStatusCode.OK, outsideArea.StatusCode);
        var outsideBody = await ReadJsonAsync(outsideArea);
        Assert.True(Property(outsideBody.RootElement, "skipped").GetBoolean());

        var unverifiedHeartbeat = await client.PostAsJsonAsync(
            "/api/heartbeat",
            new { MaThietBi = "heartbeat-device-01", Lat = 10.7589, Lng = 106.7018, PoiIdHienTai = farPoiId });
        Assert.Equal(HttpStatusCode.OK, unverifiedHeartbeat.StatusCode);

        await factory.AssertAsync(async db =>
        {
            var location = await db.VitriKhachs.SingleAsync(v => v.MaThietBi == "heartbeat-device-01");
            Assert.Null(location.PoiIdHienTai);
            Assert.Null(location.TenPoiHienTai);
        });

        var verifiedHeartbeat = await client.PostAsJsonAsync(
            "/api/heartbeat",
            new { MaThietBi = "heartbeat-device-01", Lat = 10.7589, Lng = 106.7018, PoiIdHienTai = activePoiId });
        Assert.Equal(HttpStatusCode.OK, verifiedHeartbeat.StatusCode);

        var firstVisit = await client.PostAsJsonAsync(
            "/api/heartbeat/visit",
            new { MaThietBi = "heartbeat-device-01", PoiId = activePoiId, NgonNgu = "vi-VN" });
        var duplicateVisit = await client.PostAsJsonAsync(
            "/api/heartbeat/visit",
            new { MaThietBi = "heartbeat-device-01", PoiId = activePoiId, NgonNgu = "vi" });
        var firstView = await client.PostAsJsonAsync(
            "/api/heartbeat/view",
            new { MaThietBi = "heartbeat-device-01", PoiId = activePoiId, NgonNgu = "en-US" });
        var duplicateView = await client.PostAsJsonAsync(
            "/api/heartbeat/view",
            new { MaThietBi = "heartbeat-device-01", PoiId = activePoiId, NgonNgu = "en" });

        Assert.True(Property((await ReadJsonAsync(firstVisit)).RootElement, "recorded").GetBoolean());
        Assert.False(Property((await ReadJsonAsync(duplicateVisit)).RootElement, "recorded").GetBoolean());
        Assert.True(Property((await ReadJsonAsync(firstView)).RootElement, "recorded").GetBoolean());
        Assert.False(Property((await ReadJsonAsync(duplicateView)).RootElement, "recorded").GetBoolean());

        var sync = await client.PostAsJsonAsync(
            "/api/heartbeat/sync-history",
            new
            {
                MaThietBi = "heartbeat-device-01",
                ViewedPoiIds = new[] { activePoiId, secondPoiId },
                VisitedPoiIds = new[] { activePoiId, secondPoiId },
                NgonNgu = "vi"
            });
        Assert.Equal(HttpStatusCode.OK, sync.StatusCode);
        var syncBody = await ReadJsonAsync(sync);
        Assert.Equal(1, Property(syncBody.RootElement, "insertedViews").GetInt32());
        Assert.Equal(1, Property(syncBody.RootElement, "insertedVisits").GetInt32());

        var profile = await client.GetFromJsonAsync<JsonDocument>("/api/heartbeat/profile/heartbeat-device-01");
        Assert.NotNull(profile);
        Assert.Equal(2, Property(profile!.RootElement, "ViewedPoiCount").GetInt32());
        Assert.Equal(2, Property(profile.RootElement, "VisitedPoiCount").GetInt32());
        Assert.Equal(300, Property(profile.RootElement, "ExperiencePoints").GetInt32());

        var active = await client.GetFromJsonAsync<JsonDocument>("/api/heartbeat/active");
        Assert.NotNull(active);
        Assert.Equal(1, Property(active!.RootElement, "Count").GetInt32());
        var activeItem = Property(active.RootElement, "Items").EnumerateArray().Single();
        Assert.Equal("Heartbeat Active POI", Property(activeItem, "TenPoiHienTai").GetString());
        Assert.Equal(2, Property(activeItem, "SoQuanDaGhe").GetInt32());
        Assert.Equal(2, Property(activeItem, "SoQuanDaXem").GetInt32());

        var history = await client.GetFromJsonAsync<JsonDocument>("/api/heartbeat/history/HEARTBEA");
        Assert.NotNull(history);
        Assert.Equal(2, Property(history!.RootElement, "SoDiemDaGhe").GetInt32());
        Assert.Equal("HEARTBEA", Property(history.RootElement, "DeviceShort").GetString());
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
    private const string ProgramStartupConnectionString =
        "Host=localhost;Port=5432;Database=vinhkhanhtour_test;Username=postgres;Password=postgres;Timeout=1;Command Timeout=1;Pooling=false";

    private readonly bool _usePostgres = string.Equals(
        Environment.GetEnvironmentVariable("API_TEST_DATABASE"),
        "postgres",
        StringComparison.OrdinalIgnoreCase);
    private readonly string? _originalSupabaseConnectionString;
    private readonly bool _changedSupabaseConnectionString;
    private readonly SqliteConnection? _sqliteConnection;
    private readonly string? _postgresAdminConnectionString;
    private readonly string? _postgresConnectionString;
    private readonly string? _postgresDatabaseName;

    public ApiTestApplicationFactory()
    {
        _originalSupabaseConnectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING");

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
            _changedSupabaseConnectionString = SetProgramStartupConnectionString(_postgresConnectionString);
            return;
        }

        if (string.IsNullOrWhiteSpace(_originalSupabaseConnectionString))
            _changedSupabaseConnectionString = SetProgramStartupConnectionString(ProgramStartupConnectionString);

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

        if (_usePostgres
            && !string.IsNullOrWhiteSpace(_postgresAdminConnectionString)
            && !string.IsNullOrWhiteSpace(_postgresDatabaseName))
        {
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

        if (_changedSupabaseConnectionString)
            Environment.SetEnvironmentVariable("SUPABASE_CONNECTION_STRING", _originalSupabaseConnectionString);
    }

    private static string QuoteIdentifier(string identifier)
        => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static bool SetProgramStartupConnectionString(string connectionString)
    {
        Environment.SetEnvironmentVariable("SUPABASE_CONNECTION_STRING", connectionString);
        return true;
    }
}
