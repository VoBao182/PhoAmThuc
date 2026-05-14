using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.CMS.E2ETests;

public sealed class CmsE2ETests : IAsyncLifetime
{
    private const string CmsAdminUsername = "e2e-admin";
    private const string CmsAdminPassword = "e2e-password";

    private readonly string _baseUrl = $"http://127.0.0.1:{GetAvailablePort()}";
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"vinhkhanhtour-cms-e2e-{Guid.NewGuid():N}.db");
    private readonly string _artifactRoot = Path.Combine(FindRepoRoot(), "TestResults", "artifacts", "cms");

    private Process? _cmsProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private Guid _activePoiId;
    private Guid _overduePoiId;
    private Guid _expiringPoiId;
    private Guid _approveRequestId;
    private Guid _rejectRequestId;

    [Fact]
    public async Task Cms_AdminPages_RedirectUnauthenticatedUsersToLogin()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_AdminPages_RedirectUnauthenticatedUsersToLogin), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/DuyetThanhToan",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            Assert.Contains("/Login", page.Url, StringComparison.OrdinalIgnoreCase);
            await ExpectBodyContainsAsync(page, "Vinh Khanh CMS");
            await ExpectBodyContainsAsync(page, "Dang nhap");
        }, signIn: false);
    }

    [Fact]
    public async Task Cms_LoginRejectsInvalidCredentials_AndLogoutEndsSession()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_LoginRejectsInvalidCredentials_AndLogoutEndsSession), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/Login?ReturnUrl=%2FPoi",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await page.Locator("input[name='Username']").FillAsync(CmsAdminUsername);
            await page.Locator("input[name='Password']").FillAsync("wrong-password");
            await page.Locator("form button[type='submit']").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            Assert.Contains("/Login", page.Url, StringComparison.OrdinalIgnoreCase);
            await ExpectBodyContainsAsync(page, "Tài khoản hoặc mật khẩu không đúng");

            await page.Locator("input[name='Password']").FillAsync(CmsAdminPassword);
            await page.Locator("form button[type='submit']").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            Assert.Contains("/Poi", page.Url, StringComparison.OrdinalIgnoreCase);
            await ExpectBodyContainsAsync(page, "CMS Seed Active");

            await page.Locator("[data-testid='cms-logout']").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            Assert.Contains("/Login", page.Url, StringComparison.OrdinalIgnoreCase);

            await page.GotoAsync(
                $"{_baseUrl}/Poi",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            Assert.Contains("/Login", page.Url, StringComparison.OrdinalIgnoreCase);
        }, signIn: false);
    }

    [Fact]
    public async Task Cms_DashboardAndPoiList_RenderSeededData()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_DashboardAndPoiList_RenderSeededData), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/health",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 10000 });
            await ExpectBodyContainsAsync(page, "ok");

            await page.GotoAsync(
                $"{_baseUrl}/Poi",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await ExpectBodyContainsAsync(page, "CMS Seed Active");
            await ExpectBodyContainsAsync(page, "CMS Seed Overdue");
            await ExpectBodyContainsAsync(page, "Seeded Noodle");

            await page.Locator("input[name='search']").FillAsync("Active");
            await page.Locator("form[method='get'] button[type='submit']").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await ExpectBodyContainsAsync(page, "CMS Seed Active");
            await ExpectBodyNotContainsAsync(page, "CMS Seed Overdue");
        });
    }

    [Fact]
    public async Task Cms_DashboardAnalyticsRangeFilters_RenderSeededActivityAndRevenue()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_DashboardAnalyticsRangeFilters_RenderSeededActivityAndRevenue), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/?mode=last7",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await ExpectBodyContainsAsync(page, "7 ngày qua");
            await ExpectBodyContainsAsync(page, "CMS Seed Active");
            await ExpectBodyContainsAsync(page, "228,000");
            await ExpectBodyContainsAsync(page, "CMS Seed Active");

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await page.GotoAsync(
                $"{_baseUrl}/?mode=custom&from={today}&to={today}",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await ExpectBodyContainsAsync(page, DateTime.UtcNow.ToString("dd/MM/yyyy"));
            Assert.Equal(today, await page.Locator("input[name='from']").InputValueAsync());
            Assert.Equal(today, await page.Locator("input[name='to']").InputValueAsync());
        });
    }

    [Fact]
    public async Task Cms_PoiFiltersSortingAndToggle_WorkFromAdminList()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_PoiFiltersSortingAndToggle_WorkFromAdminList), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/Poi?status=visible",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "CMS Seed Active");
            await ExpectBodyContainsAsync(page, "CMS Seed Expiring");
            await ExpectBodyNotContainsAsync(page, "CMS Seed Overdue");

            await page.GotoAsync(
                $"{_baseUrl}/Poi?status=hidden",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "CMS Seed Overdue");
            await ExpectBodyNotContainsAsync(page, "CMS Seed Active");

            await page.GotoAsync(
                $"{_baseUrl}/Poi?expiry=expiring7",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "CMS Seed Expiring");
            await ExpectBodyNotContainsAsync(page, "CMS Seed Active");
            await ExpectBodyNotContainsAsync(page, "CMS Seed Overdue");

            await page.GotoAsync(
                $"{_baseUrl}/Poi?search=CMS%20Seed&sort=name&dir=desc",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            var body = await GetBodyTextAsync(page);
            Assert.True(
                body.IndexOf("CMS Seed Overdue", StringComparison.OrdinalIgnoreCase)
                    < body.IndexOf("CMS Seed Expiring", StringComparison.OrdinalIgnoreCase),
                "Name descending sort should place Overdue before Expiring.");

            var activeRow = page.Locator("tr").Filter(new LocatorFilterOptions { HasTextString = "CMS Seed Active" }).First;
            await activeRow.Locator("form[method='post'] button").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await using var db = CreateDbContext();
            var toggled = await db.POIs.SingleAsync(p => p.Id == _activePoiId);
            Assert.False(toggled.TrangThai);

            await page.GotoAsync(
                $"{_baseUrl}/Poi?status=hidden&search=Active",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "CMS Seed Active");
        });
    }

    [Fact]
    public async Task Cms_PoiCreateAndEdit_PersistsChanges()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_PoiCreateAndEdit_PersistsChanges), async page =>
        {
            var poiName = $"CMS Created {Guid.NewGuid():N}"[..20];
            var updatedName = $"{poiName} Updated";

            await page.GotoAsync(
                $"{_baseUrl}/Poi/Create",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await page.Locator("[name='POI.TenPOI']").FillAsync(poiName);
            await page.Locator("[name='POI.SDT']").FillAsync("0909000001");
            await page.Locator("[name='POI.DiaChi']").FillAsync("123 Vinh Khanh");
            await page.Locator("[name='POI.ViDo']").FillAsync("10.7589");
            await page.Locator("[name='POI.KinhDo']").FillAsync("106.7018");
            await page.Locator("[name='POI.BanKinh']").FillAsync("35");
            await page.Locator("[name='POI.MucUuTien']").FillAsync("7");
            await page.Locator("[name='ThuyetMinhVi']").FillAsync("Noi dung test automation.");
            await SetElementValueAsync(page, "[name='ThuyetMinhEn']", "English narration from automation.");
            await SetElementValueAsync(page, "[name='ThuyetMinhZh']", "Chinese narration from automation.");
            await page.Locator("button[onclick='addMonAn()']").ClickAsync();
            await page.Locator("[name='MonAns[0].TenMonAn']").FillAsync("Automation Bun Bo");
            await page.Locator("[name='MonAns[0].MoTa']").FillAsync("Spicy test dish");
            await page.Locator("[name='MonAns[0].PhanLoai']").FillAsync("Main");
            await page.Locator("[name='MonAns[0].DonGia']").FillAsync("75000");
            await page.Locator("main form button[type='submit']").Last.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await ExpectBodyContainsAsync(page, poiName);
            await ExpectBodyContainsAsync(page, "Automation Bun Bo");

            var row = page.Locator("tr").Filter(new LocatorFilterOptions { HasTextString = poiName }).First;
            await row.Locator("a[href*='/Poi/Edit/']").First.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.Locator("[name='POI.TenPOI']").FillAsync(updatedName);
            await page.Locator("[name='POI.SDT']").FillAsync("0909000002");
            await page.Locator("[name='POI.DiaChi']").FillAsync("456 Updated Street");
            await page.Locator("[name='POI.ViDo']").FillAsync("10.7599");
            await page.Locator("[name='POI.KinhDo']").FillAsync("106.7029");
            await page.Locator("[name='POI.BanKinh']").FillAsync("55");
            await page.Locator("[name='POI.MucUuTien']").FillAsync("3");
            await page.Locator("[name='ThuyetMinhVi']").FillAsync("Noi dung da cap nhat.");
            await SetElementValueAsync(page, "[name='ThuyetMinhEn']", "Updated English narration.");
            await SetElementValueAsync(page, "[name='ThuyetMinhZh']", "Updated Chinese narration.");
            await page.Locator(".monan-item").First.Locator(".remove-btn").ClickAsync();
            await page.Locator("button[onclick='addMonAn()']").ClickAsync();
            await page.Locator("[name='MonAns[1].TenMonAn']").FillAsync("Automation Oc Len");
            await page.Locator("[name='MonAns[1].MoTa']").FillAsync("Replacement dish");
            await page.Locator("[name='MonAns[1].PhanLoai']").FillAsync("Seafood");
            await page.Locator("[name='MonAns[1].DonGia']").FillAsync("99000");
            await page.Locator("main form button[type='submit']").Last.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await ExpectBodyContainsAsync(page, updatedName);

            await using var db = CreateDbContext();
            var saved = await db.POIs
                .Include(p => p.MonAns)
                .Include(p => p.ThuyetMinhs)
                    .ThenInclude(t => t.BanDichs)
                .SingleAsync(p => p.TenPOI == updatedName);
            Assert.Equal("0909000002", saved.SDT);
            Assert.Equal("456 Updated Street", saved.DiaChi);
            Assert.Equal(10.7599, saved.ViDo, 4);
            Assert.Equal(106.7029, saved.KinhDo, 4);
            Assert.Equal(55, saved.BanKinh);
            Assert.Equal(3, saved.MucUuTien);
            Assert.True(saved.NgayHetHanDuyTri > DateTime.UtcNow);
            Assert.Contains(saved.ThuyetMinhs.SelectMany(t => t.BanDichs), b => b.NgonNgu == "vi" && b.NoiDung == "Noi dung da cap nhat.");
            Assert.Contains(saved.ThuyetMinhs.SelectMany(t => t.BanDichs), b => b.NgonNgu == "en" && b.NoiDung == "Updated English narration.");
            Assert.Contains(saved.ThuyetMinhs.SelectMany(t => t.BanDichs), b => b.NgonNgu == "zh" && b.NoiDung == "Updated Chinese narration.");
            Assert.Contains(saved.MonAns, m => m.TenMonAn == "Automation Bun Bo" && !m.TinhTrang);
            Assert.Contains(saved.MonAns, m => m.TenMonAn == "Automation Oc Len" && m.TinhTrang && m.DonGia == 99_000m);
        });
    }

    [Fact]
    public async Task Cms_PoiCreate_RequiresNameBeforeSubmitting()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_PoiCreate_RequiresNameBeforeSubmitting), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/Poi/Create",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await page.Locator("[name='POI.SDT']").FillAsync("0909000999");
            await page.Locator("[name='POI.DiaChi']").FillAsync("Missing name street");
            await page.Locator("[name='POI.ViDo']").FillAsync("10.7589");
            await page.Locator("[name='POI.KinhDo']").FillAsync("106.7018");
            await page.Locator("[name='POI.BanKinh']").FillAsync("35");

            Assert.False(await page.Locator("[name='POI.TenPOI']").EvaluateAsync<bool>("el => el.checkValidity()"));
            await page.Locator("main form button[type='submit']").Last.ClickAsync();

            Assert.Contains("/Poi/Create", page.Url, StringComparison.OrdinalIgnoreCase);
            await using var db = CreateDbContext();
            Assert.False(await db.POIs.AnyAsync(p => p.SDT == "0909000999"));
        });
    }

    [Fact]
    public async Task Cms_ThuyetMinhList_ShowsTranslationsAndOpensPoiEdit()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_ThuyetMinhList_ShowsTranslationsAndOpensPoiEdit), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/ThuyetMinh",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await ExpectBodyContainsAsync(page, "CMS Seed Active");
            await ExpectBodyContainsAsync(page, "Seed narration VI");
            await ExpectBodyContainsAsync(page, "Seed narration EN");
            await ExpectBodyContainsAsync(page, "Seed narration ZH");

            await page.Locator($"a[href='/Poi/Edit/{_activePoiId}']").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            Assert.Contains($"/Poi/Edit/{_activePoiId}", page.Url, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("CMS Seed Active", await page.Locator("[name='POI.TenPOI']").InputValueAsync());
            Assert.Equal("Seed narration VI", await page.Locator("[name='ThuyetMinhVi']").InputValueAsync());
        });
    }

    [Fact]
    public async Task Cms_MaintenancePayment_ExtendsPoiAndCreatesInvoices()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_MaintenancePayment_ExtendsPoiAndCreatesInvoices), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/ThanhToan",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await ExpectBodyContainsAsync(page, "CMS Seed Overdue");

            var row = page.Locator("tr").Filter(new LocatorFilterOptions { HasTextString = "CMS Seed Overdue" }).First;
            var renewalFormLoaded = page.Locator("select[name='SoThangGiaHan']").WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });
            await row.Locator("a[href*='/ThanhToan/GhiNhan']").First.ClickAsync();
            await renewalFormLoaded;

            await page.Locator("select[name='SoThangGiaHan']").SelectOptionAsync("2");
            await page.Locator("input[name='PhiDuyTriThang']").FillAsync("80000");
            await page.Locator("input[name='GhiChu']").FillAsync("CMS E2E renewal");
            var redirectedToPaymentList = page.WaitForURLAsync(
                url =>
                    url.Contains("/ThanhToan", StringComparison.OrdinalIgnoreCase)
                    && !url.Contains("/ThanhToan/GhiNhan", StringComparison.OrdinalIgnoreCase),
                new PageWaitForURLOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await page.Locator("main form button[type='submit']").Last.ClickAsync();
            await redirectedToPaymentList;

            await ExpectBodyContainsAsync(page, "CMS Seed Overdue");
            await ExpectBodyContainsAsync(page, "160");

            await using var db = CreateDbContext();
            var poi = await db.POIs.SingleAsync(p => p.Id == _overduePoiId);
            Assert.True(poi.TrangThai);
            Assert.True(poi.NgayHetHanDuyTri > DateTime.UtcNow.AddDays(20));

            var invoices = await db.HoaDons
                .Where(h => h.POIId == _overduePoiId && h.LoaiPhi == "duytri")
                .ToListAsync();

            Assert.Equal(2, invoices.Count);
            Assert.All(invoices, invoice => Assert.Equal(80_000m, invoice.SoTien));
            Assert.All(invoices, invoice => Assert.Equal("CMS E2E renewal", invoice.GhiChu));

            await page.GotoAsync(
                $"{_baseUrl}/ThanhToan/LichSu/{_overduePoiId}",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "CMS Seed Overdue");
            await ExpectBodyContainsAsync(page, "CMS E2E renewal");
            await ExpectBodyContainsAsync(page, "80,000");
            await ExpectBodyContainsAsync(page, "160,000");
        });
    }

    [Fact]
    public async Task Cms_ThanhToanClientFilter_ShowsOnlySelectedPaymentStatusRows()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_ThanhToanClientFilter_ShowsOnlySelectedPaymentStatusRows), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/ThanhToan",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await page.Locator("#filterStatus").SelectOptionAsync("quahan");
            var overdueRows = await GetVisibleRowTextsAsync(page, "#poiTable tbody tr");
            Assert.Contains(overdueRows, row => row.Contains("CMS Seed Overdue", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(overdueRows, row => row.Contains("CMS Seed Active", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(overdueRows, row => row.Contains("CMS Seed Expiring", StringComparison.OrdinalIgnoreCase));

            await page.Locator("#filterStatus").SelectOptionAsync("saphan");
            var expiringRows = await GetVisibleRowTextsAsync(page, "#poiTable tbody tr");
            Assert.Contains(expiringRows, row => row.Contains("CMS Seed Expiring", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(expiringRows, row => row.Contains("CMS Seed Overdue", StringComparison.OrdinalIgnoreCase));

            await page.Locator("#filterStatus").SelectOptionAsync("ok");
            var okRows = await GetVisibleRowTextsAsync(page, "#poiTable tbody tr");
            Assert.Contains(okRows, row => row.Contains("CMS Seed Active", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(okRows, row => row.Contains("CMS Seed Overdue", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task Cms_PaymentApprovalFiltersAndRejectValidation_WorkForAdmin()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_PaymentApprovalFiltersAndRejectValidation_WorkForAdmin), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/DuyetThanhToan?tab=cho_duyet&pkg=tuan&search=E2EAPP",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "VKT TUAN E2EAPP");
            await ExpectBodyNotContainsAsync(page, "VKT NGAY E2EREJ");

            var stayOnPending = page.WaitForURLAsync(
                url => url.Contains("tab=cho_duyet", StringComparison.OrdinalIgnoreCase)
                    && url.Contains("err=", StringComparison.OrdinalIgnoreCase),
                new PageWaitForURLOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await page.EvaluateAsync(
                @"id => {
                    const form = document.querySelector('#modalTuChoi form');
                    document.querySelector('#tuChoiId').value = id;
                    const textarea = form.querySelector('textarea[name=""lyDo""]');
                    textarea.required = false;
                    textarea.value = '';
                    HTMLFormElement.prototype.submit.call(form);
                }",
                _rejectRequestId.ToString());
            await stayOnPending;

            await ExpectBodyContainsAsync(page, "Ly do tu choi la bat buoc.");

            await using (var db = CreateDbContext())
            {
                var stillPending = await db.YeuCauThanhToans.SingleAsync(y => y.Id == _rejectRequestId);
                Assert.Equal("cho_duyet", stillPending.TrangThai);
                Assert.Null(stillPending.GhiChuAdmin);
            }

            page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
            var approveRow = page.Locator("tr").Filter(new LocatorFilterOptions { HasTextString = "VKT TUAN E2EAPP" }).First;
            await approveRow.Locator("form[action*='Approve'] button[type='submit'], form button.action-approve").First.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.GotoAsync(
                $"{_baseUrl}/DuyetThanhToan?tab=da_duyet&pkg=tuan&search=E2EAPP",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "VKT TUAN E2EAPP");
            await ExpectBodyNotContainsAsync(page, "VKT NGAY E2EREJ");

            await using (var db = CreateDbContext())
            {
                var subscription = await db.DangKyApps.SingleAsync(d => d.MaThietBi == "e2eapp-device-01");
                Assert.Equal("tuan", subscription.LoaiGoi);
                Assert.True(subscription.NgayHetHan > DateTime.UtcNow.AddDays(6));
            }
        });
    }

    [Fact]
    public async Task Cms_PendingPaymentSnapshot_ReturnsLatestPendingRequestForPolling()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_PendingPaymentSnapshot_ReturnsLatestPendingRequestForPolling), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/DuyetThanhToan?handler=PendingSnapshot",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            var json = await page.Locator("body").InnerTextAsync();
            using var snapshot = JsonDocument.Parse(json);
            Assert.True(Property(snapshot.RootElement, "Ok").GetBoolean());
            Assert.Equal(2, Property(snapshot.RootElement, "Count").GetInt32());
            Assert.Equal(_rejectRequestId.ToString("N"), Property(snapshot.RootElement, "LatestId").GetString());
            Assert.Equal("VKT NGAY E2EREJ", Property(snapshot.RootElement, "LatestTransferContent").GetString());
            Assert.Equal("E2EREJ-D", Property(snapshot.RootElement, "LatestDeviceShort").GetString());
        });
    }

    [Fact]
    public async Task Cms_PaymentApprovalAndRejection_UpdateRequestsAndSubscription()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_PaymentApprovalAndRejection_UpdateRequestsAndSubscription), async page =>
        {
            page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

            await page.GotoAsync(
                $"{_baseUrl}/DuyetThanhToan",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await ExpectBodyContainsAsync(page, "VKT TUAN E2EAPP");
            await ExpectBodyContainsAsync(page, "VKT NGAY E2EREJ");

            var approveRow = page.Locator("tr").Filter(new LocatorFilterOptions { HasTextString = "VKT TUAN E2EAPP" }).First;
            await approveRow.Locator("form[action*='Approve'] button[type='submit'], form button.action-approve").First.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await ExpectBodyContainsAsync(page, "99,000");

            await page.GotoAsync(
                $"{_baseUrl}/DuyetThanhToan",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await ExpectBodyContainsAsync(page, "VKT NGAY E2EREJ");

            var rejectedPage = page.WaitForURLAsync(
                url => url.Contains("tab=tu_choi", StringComparison.OrdinalIgnoreCase),
                new PageWaitForURLOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

            await page.EvaluateAsync(
                @"([id, reason]) => {
                    document.querySelector('#tuChoiId').value = id;
                    document.querySelector('#modalTuChoi textarea[name=""lyDo""]').value = reason;
                    document.querySelector('#modalTuChoi form').requestSubmit();
                }",
                new[] { _rejectRequestId.ToString(), "CMS E2E reject reason" });

            await rejectedPage;

            await ExpectBodyContainsAsync(page, "CMS E2E reject reason");

            await using var db = CreateDbContext();
            var approved = await db.YeuCauThanhToans.SingleAsync(y => y.Id == _approveRequestId);
            var rejected = await db.YeuCauThanhToans.SingleAsync(y => y.Id == _rejectRequestId);
            Assert.Equal("da_duyet", approved.TrangThai);
            Assert.Equal("tu_choi", rejected.TrangThai);
            Assert.Equal("CMS E2E reject reason", rejected.GhiChuAdmin);

            var subscription = await db.DangKyApps.SingleAsync(d => d.MaThietBi == "e2eapp-device-01");
            Assert.Equal("tuan", subscription.LoaiGoi);
            Assert.True(subscription.NgayHetHan > DateTime.UtcNow.AddDays(6));
        });
    }

    [Fact]
    public async Task Cms_BanDoFiltersCustomersByOnlineStateAndCurrentPoi()
    {
        await RunWithPageArtifactsAsync(nameof(Cms_BanDoFiltersCustomersByOnlineStateAndCurrentPoi), async page =>
        {
            await page.GotoAsync(
                $"{_baseUrl}/BanDo?filter=online&search=map-device",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "map-device-online-01");
            await ExpectBodyContainsAsync(page, "CMS Seed Active");
            await ExpectBodyContainsAsync(page, "150 XP");
            await ExpectBodyNotContainsAsync(page, "map-device-offline-01");

            await page.GotoAsync(
                $"{_baseUrl}/BanDo?filter=offline&search=map-device",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "map-device-offline-01");
            await ExpectBodyNotContainsAsync(page, "map-device-online-01");

            await page.GotoAsync(
                $"{_baseUrl}/BanDo?filter=at-poi&search=CMS%20Seed%20Active",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
            await ExpectBodyContainsAsync(page, "map-device-online-01");
            await ExpectBodyContainsAsync(page, "CMS Seed Active");
            await ExpectBodyNotContainsAsync(page, "map-device-offline-01");
        });
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_artifactRoot);
        await SeedDatabaseAsync();
        await StartCmsAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();

        if (_cmsProcess is not null && !_cmsProcess.HasExited)
        {
            _cmsProcess.Kill(entireProcessTree: true);
            await _cmsProcess.WaitForExitAsync();
        }

        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch
        {
            // Best-effort cleanup. Test artifacts are more important than temp DB deletion.
        }
    }

    private async Task RunWithPageArtifactsAsync(string testName, Func<IPage, Task> test, bool signIn = true)
    {
        if (_browser is null)
            throw new InvalidOperationException("Browser was not initialized.");

        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 1000 }
        });
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        var page = await context.NewPageAsync();
        try
        {
            if (signIn)
                await SignInAsync(page);

            await test(page);
            await context.Tracing.StopAsync();
        }
        catch
        {
            await SavePageArtifactsAsync(page, context, testName);
            throw;
        }
    }

    private async Task SignInAsync(IPage page)
    {
        await page.GotoAsync(
            $"{_baseUrl}/Login",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });

        await page.Locator("input[name='Username']").FillAsync(CmsAdminUsername);
        await page.Locator("input[name='Password']").FillAsync(CmsAdminPassword);
        await page.Locator("form button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        Assert.DoesNotContain("/Login", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SavePageArtifactsAsync(IPage page, IBrowserContext context, string testName)
    {
        var safeName = string.Concat(testName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var prefix = Path.Combine(_artifactRoot, $"{safeName}-{timestamp}");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = $"{prefix}.png",
            FullPage = true
        });
        await File.WriteAllTextAsync($"{prefix}.html", await page.ContentAsync());
        await context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = $"{prefix}.zip"
        });
    }

    private async Task SeedDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        _activePoiId = Guid.NewGuid();
        _overduePoiId = Guid.NewGuid();
        _expiringPoiId = Guid.NewGuid();
        _approveRequestId = Guid.NewGuid();
        _rejectRequestId = Guid.NewGuid();

        var activePoi = CreatePoi(
            _activePoiId,
            "CMS Seed Active",
            "10 Active Street",
            priority: 1,
            expiresAtUtc: DateTime.UtcNow.AddDays(30),
            active: true);
        activePoi.MonAns.Add(new MonAn
        {
            Id = Guid.NewGuid(),
            POIId = _activePoiId,
            TenMonAn = "Seeded Noodle",
            PhanLoai = "Noodle",
            DonGia = 59_000m,
            TinhTrang = true
        });

        var overduePoi = CreatePoi(
            _overduePoiId,
            "CMS Seed Overdue",
            "20 Overdue Street",
            priority: 2,
            expiresAtUtc: DateTime.UtcNow.AddDays(-5),
            active: false);
        var expiringPoi = CreatePoi(
            _expiringPoiId,
            "CMS Seed Expiring",
            "30 Expiring Street",
            priority: 3,
            expiresAtUtc: DateTime.UtcNow.AddDays(3),
            active: true);

        var activeNarration = new VinhKhanhTour.API.Models.ThuyetMinh
        {
            Id = Guid.NewGuid(),
            POIId = _activePoiId,
            TrangThai = true,
            BanDichs =
            [
                new BanDich { Id = Guid.NewGuid(), NgonNgu = "vi", NoiDung = "Seed narration VI" },
                new BanDich { Id = Guid.NewGuid(), NgonNgu = "en", NoiDung = "Seed narration EN" },
                new BanDich { Id = Guid.NewGuid(), NgonNgu = "zh", NoiDung = "Seed narration ZH" }
            ]
        };
        activePoi.ThuyetMinhs.Add(activeNarration);

        db.POIs.AddRange(activePoi, overduePoi, expiringPoi);
        db.DangKyDichVus.AddRange(
            new DangKyDichVu
            {
                Id = Guid.NewGuid(),
                POIId = _activePoiId,
                PhiDuyTriThang = 50_000m,
                PhiConvert = 20_000m,
                NgayBatDau = DateTime.UtcNow.AddMonths(-1),
                NgayHetHan = DateTime.UtcNow.AddDays(30),
                TrangThai = true
            },
            new DangKyDichVu
            {
                Id = Guid.NewGuid(),
                POIId = _overduePoiId,
                PhiDuyTriThang = 80_000m,
                PhiConvert = 25_000m,
                NgayBatDau = DateTime.UtcNow.AddMonths(-2),
                NgayHetHan = DateTime.UtcNow.AddDays(-5),
                TrangThai = true
            },
            new DangKyDichVu
            {
                Id = Guid.NewGuid(),
                POIId = _expiringPoiId,
                PhiDuyTriThang = 70_000m,
                PhiConvert = 20_000m,
                NgayBatDau = DateTime.UtcNow.AddMonths(-1),
                NgayHetHan = DateTime.UtcNow.AddDays(3),
                TrangThai = true
            });

        db.YeuCauThanhToans.AddRange(
            new YeuCauThanhToan
            {
                Id = _approveRequestId,
                MaThietBi = "e2eapp-device-01",
                LoaiGoi = "tuan",
                SoTien = 99_000m,
                NoiDungChuyen = "VKT TUAN E2EAPP",
                TrangThai = "cho_duyet",
                NgayTao = DateTime.UtcNow.AddMinutes(-5)
            },
            new YeuCauThanhToan
            {
                Id = _rejectRequestId,
                MaThietBi = "e2erej-device-01",
                LoaiGoi = "ngay",
                SoTien = 29_000m,
                NoiDungChuyen = "VKT NGAY E2EREJ",
                TrangThai = "cho_duyet",
                NgayTao = DateTime.UtcNow.AddMinutes(-3)
            });
        db.DangKyApps.AddRange(
            new DangKyApp
            {
                Id = Guid.NewGuid(),
                MaThietBi = "map-device-online-01",
                LoaiGoi = "thang",
                NgayBatDau = DateTime.UtcNow.AddDays(-1),
                NgayHetHan = DateTime.UtcNow.AddDays(29),
                SoTien = 199_000m
            },
            new DangKyApp
            {
                Id = Guid.NewGuid(),
                MaThietBi = "map-device-offline-01",
                LoaiGoi = "ngay",
                NgayBatDau = DateTime.UtcNow.AddDays(-2),
                NgayHetHan = DateTime.UtcNow.AddDays(-1),
                SoTien = 29_000m
            });
        db.VitriKhachs.AddRange(
            new VitriKhach
            {
                Id = Guid.NewGuid(),
                MaThietBi = "map-device-online-01",
                Lat = 10.7589,
                Lng = 106.7018,
                LanCuoiHeartbeat = DateTime.UtcNow.AddSeconds(-30),
                PoiIdHienTai = _activePoiId,
                TenPoiHienTai = "CMS Seed Active"
            },
            new VitriKhach
            {
                Id = Guid.NewGuid(),
                MaThietBi = "map-device-offline-01",
                Lat = 10.7591,
                Lng = 106.7021,
                LanCuoiHeartbeat = DateTime.UtcNow.AddMinutes(-20),
                PoiIdHienTai = null,
                TenPoiHienTai = null
            });
        db.LichSuPhats.AddRange(
            new LichSuPhat
            {
                Id = Guid.NewGuid(),
                POIId = _activePoiId,
                MaThietBi = "map-device-online-01",
                Nguon = "GPS",
                NgonNguDung = "vi",
                ThoiGian = DateTime.UtcNow.AddMinutes(-2)
            },
            new LichSuPhat
            {
                Id = Guid.NewGuid(),
                POIId = _expiringPoiId,
                MaThietBi = "map-device-online-01",
                Nguon = "VIEW",
                NgonNguDung = "vi",
                ThoiGian = DateTime.UtcNow.AddMinutes(-1)
            });

        await db.SaveChangesAsync();
    }

    private async Task StartCmsAsync()
    {
        var repoRoot = FindRepoRoot();
        var cmsProject = Path.Combine(repoRoot, "VinhKhanhTour.CMS", "VinhKhanhTour.CMS.csproj");
        var logPath = Path.Combine(_artifactRoot, $"cms-{Guid.NewGuid():N}.log");
        var logWriter = TextWriter.Synchronized(File.CreateText(logPath));

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(cmsProject);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.Environment["ASPNETCORE_URLS"] = _baseUrl;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["CMS_TEST_SQLITE_PATH"] = _databasePath;
        startInfo.Environment["CMS_ADMIN_USERNAME"] = CmsAdminUsername;
        startInfo.Environment["CMS_ADMIN_PASSWORD"] = CmsAdminPassword;
        startInfo.Environment["Logging__LogLevel__Default"] = "Warning";
        startInfo.Environment["Logging__LogLevel__Microsoft.AspNetCore"] = "Warning";
        startInfo.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Warning";

        _cmsProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start CMS process.");
        _cmsProcess.OutputDataReceived += (_, e) => { if (e.Data is not null) logWriter.WriteLine(e.Data); };
        _cmsProcess.ErrorDataReceived += (_, e) => { if (e.Data is not null) logWriter.WriteLine(e.Data); };
        _cmsProcess.BeginOutputReadLine();
        _cmsProcess.BeginErrorReadLine();

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (_cmsProcess.HasExited)
                throw new InvalidOperationException($"CMS process exited before it became ready. See log: {logPath}");

            if (await IsHealthyAsync(_baseUrl))
                return;

            await Task.Delay(700);
        }

        throw new TimeoutException($"CMS did not become ready at {_baseUrl}. See log: {logPath}");
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        return new AppDbContext(options);
    }

    private static POI CreatePoi(Guid id, string name, string address, int priority, DateTime expiresAtUtc, bool active)
        => new()
        {
            Id = id,
            TenPOI = name,
            DiaChi = address,
            SDT = "0909000000",
            KinhDo = 106.7018,
            ViDo = 10.7589,
            BanKinh = 35,
            MucUuTien = priority,
            TrangThai = active,
            NgayHetHanDuyTri = expiresAtUtc
        };

    private static async Task SetElementValueAsync(IPage page, string selector, string value)
    {
        await page.Locator(selector).EvaluateAsync(
            "(element, nextValue) => { element.value = nextValue; element.dispatchEvent(new Event('input', { bubbles: true })); }",
            value);
    }

    private static async Task<string[]> GetVisibleRowTextsAsync(IPage page, string selector)
        => await page.Locator(selector).EvaluateAllAsync<string[]>(
            "rows => rows.filter(row => getComputedStyle(row).display !== 'none').map(row => row.innerText)");

    private static async Task<string> GetBodyTextAsync(IPage page)
        => await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 10000 });

    private static JsonElement Property(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value))
            return value;

        var camelName = char.ToLowerInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(camelName, out value))
            return value;

        throw new KeyNotFoundException($"JSON property '{name}' was not found.");
    }

    private static async Task ExpectBodyContainsAsync(IPage page, string expected)
    {
        var body = await GetBodyTextAsync(page);
        Assert.Contains(expected, body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ExpectBodyNotContainsAsync(IPage page, string unexpected)
    {
        var body = await GetBodyTextAsync(page);
        Assert.DoesNotContain(unexpected, body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> IsHealthyAsync(string baseUrl)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        try
        {
            var response = await http.GetAsync($"{baseUrl.TrimEnd('/')}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot([CallerFilePath] string sourcePath = "")
    {
        var configuredRoot = Environment.GetEnvironmentVariable("TEST_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot)
            && File.Exists(Path.Combine(configuredRoot, "VinhKhanhTourDemo.slnx")))
        {
            return configuredRoot;
        }

        var currentDirectory = new DirectoryInfo(Environment.CurrentDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "VinhKhanhTourDemo.slnx")))
                return currentDirectory.FullName;

            currentDirectory = currentDirectory.Parent;
        }

        var sourceDirectory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? "");
        while (sourceDirectory is not null)
        {
            if (File.Exists(Path.Combine(sourceDirectory.FullName, "VinhKhanhTourDemo.slnx")))
                return sourceDirectory.FullName;

            sourceDirectory = sourceDirectory.Parent;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VinhKhanhTourDemo.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }
}
