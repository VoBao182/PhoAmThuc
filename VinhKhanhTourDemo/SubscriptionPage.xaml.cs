using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanhTourDemo;

public partial class SubscriptionPage : ContentPage
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    {
        Timeout = AppConfig.PreferredApiRequestTimeout
    };

    private const string PrefNgayHetHan = "sub_ngay_het_han";
    private const string PrefDaDungThu = "da_dung_thu";

    public SubscriptionPage(bool hetHan = false)
    {
        InitializeComponent();
        ApplyLocalizedUiText();
        BannerHetHan.IsVisible = hetHan;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUiText();
        UpdateRecoveryCard();
        UpdateTrialButtonState();
        HideApiGuide();
    }

    private void ApplyLocalizedUiText()
    {
        LblPlanHeader.Text = AppText.T("GÓI", "PLANS", "套餐");
        LblPlanSubtitle.Text = AppText.T(
            "Chọn gói phù hợp để bắt đầu khám phá",
            "Choose a plan to start exploring",
            "选择套餐，开始探索");
        LblExpiredBanner.Text = AppText.T(
            "Gói sử dụng đã hết hạn. Vui lòng gia hạn để tiếp tục.",
            "Your plan has expired. Please renew to continue.",
            "套餐已过期。请续费后继续使用。");

        LblApiGuideTitle.Text = AppText.T("Kết nối thanh toán", "Payment connection", "支付连接");
        BtnConfigureApi.Text = AppText.T("Nhập API URL", "Enter API URL", "输入 API URL");

        LblTrialName.Text = AppText.T("Dùng thử", "Free Trial", "免费试用");
        LblTrialBadge.Text = AppText.T("MIỄN PHÍ", "FREE", "免费");
        LblTrialDescription.Text = AppText.T(
            "3 ngày trải nghiệm đầy đủ",
            "3 days of full access",
            "3 天完整体验");
        LblPaidPlansTitle.Text = AppText.T("GÓI TRẢ PHÍ", "PAID PLANS", "付费套餐");

        LblDayPlanName.Text = AppText.PlanName("ngay");
        LblDayPlanDescription.Text = AppText.T(
            "1 ngày khám phá thoải mái",
            "1 day of easy exploring",
            "1 天轻松探索");
        LblWeekPlanName.Text = AppText.PlanName("tuan");
        LblWeekPlanDescription.Text = AppText.T(
            "7 ngày trải nghiệm liên tục",
            "7 days of continuous access",
            "7 天连续体验");
        LblMonthPlanName.Text = AppText.PlanName("thang");
        LblPopularBadge.Text = AppText.T("PHỔ BIẾN", "POPULAR", "热门");
        LblMonthPlanDescription.Text = AppText.T(
            "30 ngày không giới hạn",
            "30 days unlimited",
            "30 天不限量");
        LblYearPlanName.Text = AppText.PlanName("nam");
        LblSavingBadge.Text = AppText.T("TIẾT KIỆM", "SAVE", "省钱");
        LblYearPlanDescription.Text = AppText.T(
            "365 ngày - Tốt nhất cho khám phá",
            "365 days - Best for exploring",
            "365 天 - 最适合探索");

        var buyNow = AppText.T("Mua ngay", "Buy now", "立即购买");
        BtnMuaNgay.Text = buyNow;
        BtnMuaTuan.Text = buyNow;
        BtnMuaThang.Text = buyNow;
        BtnMuaNam.Text = buyNow;

        LblRecoveryTitle.Text = AppText.T("Khôi phục dữ liệu", "Restore data", "恢复数据");
        LblRecoverySubtitle.Text = AppText.T(
            "Dùng khi cài lại app hoặc đổi thiết bị",
            "Use after reinstalling or changing devices",
            "重装应用或更换设备时使用");
        BtnScanRecoveryQr.Text = AppText.T("Quét QR", "Scan QR", "扫描 QR");
        BtnEnterRecoveryCode.Text = AppText.T("Nhập mã", "Enter code", "输入代码");
        BtnPasteRecoveryCode.Text = AppText.T("Dán mã", "Paste code", "粘贴代码");
        BtnCopyRecoveryCode.Text = AppText.T("Copy mã hiện tại", "Copy current code", "复制当前代码");

        LblFooterHint1.Text = AppText.T(
            "Tự động kích hoạt khi kết nối được API thanh toán",
            "Activates automatically when the payment API is reachable",
            "连接支付 API 后会自动激活");
        LblFooterHint2.Text = AppText.T(
            "Có thể test USB bằng adb reverse hoặc nhập API URL thủ công",
            "For USB testing, use adb reverse or enter the API URL manually",
            "USB 测试可使用 adb reverse 或手动输入 API URL");
        LblFooterHint3.Text = AppText.T(
            "Hỗ trợ 3 ngôn ngữ: Việt, Anh, Trung",
            "Supports Vietnamese, English, and Chinese",
            "支持越南语、英语和中文");
    }

    private void UpdateRecoveryCard()
    {
        LblRecoveryCode.Text = DeviceIdentity.BuildRecoveryPayload();
        ImgRecoveryQr.Source = ImageSource.FromUri(new Uri(DeviceIdentity.BuildQrCodeUrl()));
    }

    private void UpdateTrialButtonState()
    {
        bool daDungThu = Preferences.Get(PrefDaDungThu, false);
        BtnDungThu.IsEnabled = !daDungThu;
        BtnDungThu.Text = daDungThu
            ? AppText.T("Đã sử dụng", "Used", "已使用")
            : AppText.T("Thử ngay", "Try now", "立即试用");
        BtnDungThu.BackgroundColor = daDungThu
            ? Color.FromArgb("#9CA3AF")
            : Color.FromArgb("#22C55E");
    }

    private async void OnMuaGoiClicked(object? sender, EventArgs? e)
    {
        if (sender is not Button btn)
            return;

        var loaiGoi = btn.CommandParameter?.ToString() ?? "thang";
        var deviceId = DeviceIdentity.GetDeviceId();

        if (loaiGoi == "thu")
        {
            await ActivateFreeTrialAsync(deviceId);
            return;
        }

        await Navigation.PushModalAsync(new PaymentPage(loaiGoi), animated: true);
    }

    private void HideApiGuide()
    {
        ApiGuideCard.IsVisible = false;
    }

    private async void OnConfigureApiClicked(object? sender, EventArgs e)
    {
        var apiBaseUrl = await ApiConnectionPrompt.PromptForApiBaseUrlAsync(this, _http);
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            LblError.IsVisible = false;
            HideApiGuide();
        }
    }

    private async void OnCopyRecoveryCodeClicked(object? sender, EventArgs e)
    {
        await Clipboard.SetTextAsync(DeviceIdentity.BuildRecoveryPayload());
        LblRecoveryStatus.Text = AppText.T(
            "Đã copy mã khôi phục hiện tại.",
            "Current recovery code copied.",
            "已复制当前恢复码。");
    }

    private async void OnPasteRecoveryCodeClicked(object? sender, EventArgs e)
    {
        var code = await Clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(code))
        {
            LblRecoveryStatus.Text = AppText.T(
                "Clipboard chưa có mã khôi phục.",
                "Clipboard does not contain a recovery code.",
                "剪贴板中没有恢复码。");
            return;
        }

        await RestoreFromRecoveryCodeAsync(code);
    }

    private async void OnEnterRecoveryCodeClicked(object? sender, EventArgs e)
    {
        var code = await DisplayPromptAsync(
            AppText.T("Khôi phục dữ liệu", "Restore data", "恢复数据"),
            AppText.T(
                "Nhập mã khôi phục hoặc nội dung quét từ QR.",
                "Enter your recovery code or scanned QR content.",
                "输入恢复码或扫描 QR 得到的内容。"),
            AppText.T("Khôi phục", "Restore", "恢复"),
            AppText.T("Hủy", "Cancel", "取消"),
            "VKT-DEVICE:...");

        if (!string.IsNullOrWhiteSpace(code))
            await RestoreFromRecoveryCodeAsync(code);
    }

    private async void OnScanRecoveryQrClicked(object? sender, EventArgs e)
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            permission = await Permissions.RequestAsync<Permissions.Camera>();

        if (permission != PermissionStatus.Granted)
        {
            LblRecoveryStatus.Text = AppText.T(
                "Cần cấp quyền camera để quét QR.",
                "Camera permission is required to scan QR codes.",
                "需要相机权限才能扫描 QR。");
            return;
        }

        await Navigation.PushModalAsync(new QrScannerPage(RestoreFromRecoveryCodeAsync), animated: true);
    }

    private async Task RestoreFromRecoveryCodeAsync(string rawCode)
    {
        if (!DeviceIdentity.TrySetDeviceIdOverride(rawCode, out var restoredDeviceId))
        {
            LblRecoveryStatus.Text = AppText.T(
                "Mã khôi phục không hợp lệ.",
                "Invalid recovery code.",
                "恢复码无效。");
            return;
        }

        UpdateRecoveryCard();
        SetLoading(true);
        LblError.IsVisible = false;
        LblRecoveryStatus.Text = AppText.T(
            "Đã nhận mã cũ. Đang đồng bộ gói...",
            "Old code accepted. Syncing subscription...",
            "已接收旧代码。正在同步套餐...");

        try
        {
            var restored = await RestoreSubscriptionStateAsync(restoredDeviceId);
            UpdateTrialButtonState();

            if (restored)
            {
                await DisplayAlertAsync(
                    AppText.T("Đã khôi phục", "Restored", "已恢复"),
                    AppText.T(
                        "Ứng dụng đã đồng bộ lại gói sử dụng và mã thiết bị cũ.",
                        "The app has synced your subscription and previous device code.",
                        "应用已同步套餐和旧设备码。"),
                    AppText.T("Tiếp tục", "Continue", "继续"));

                await ExitSubscriptionGateAsync();
                return;
            }

            LblRecoveryStatus.Text = AppText.T(
                "Đã đổi sang mã cũ, nhưng mã này chưa có gói còn hạn. Bạn có thể mua/dùng thử bằng mã này.",
                "Switched to the old code, but it has no active plan. You can buy or start a trial with this code.",
                "已切换到旧代码，但该代码没有有效套餐。你可以用此代码购买或试用。");
        }
        catch (Exception ex)
        {
            LblRecoveryStatus.Text = AppConfig.BuildConnectionErrorMessage(ex);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task<bool> RestoreSubscriptionStateAsync(string deviceId)
    {
        var apiBaseUrl = await ApiConnectionPrompt.EnsureConnectedApiBaseUrlAsync(this, _http);
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            return false;

        var url = $"{apiBaseUrl}/api/subscription/status/{Uri.EscapeDataString(deviceId)}";
        var status = await _http.GetFromJsonAsync<SubscriptionStatusResponse>(url);
        if (status == null)
            return false;

        Preferences.Set(PrefDaDungThu, status.DaDungThu);

        if (status.NgayHetHan.HasValue)
            Preferences.Set(PrefNgayHetHan, status.NgayHetHan.Value.ToString("O"));

        return status.CoDangKy
            && status.NgayHetHan.HasValue
            && status.NgayHetHan.Value > DateTime.UtcNow;
    }

    private async Task ActivateFreeTrialAsync(string deviceId)
    {
        SetLoading(true);
        LblError.IsVisible = false;

        try
        {
            var apiBaseUrl = await ApiConnectionPrompt.EnsureConnectedApiBaseUrlAsync(this, _http);
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                LblError.IsVisible = true;
                LblError.Text = AppConfig.BuildConnectionErrorMessage(
                    new HttpRequestException("Unable to reach API."));
                return;
            }

            var body = new { MaThietBi = deviceId, LoaiGoi = "thu" };
            var res = await _http.PostAsJsonAsync($"{apiBaseUrl}/api/subscription/purchase", body);

            if (!res.IsSuccessStatusCode)
            {
                var errJson = await res.Content.ReadFromJsonAsync<JsonElement>();
                var errMsg = errJson.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : AppText.T("Thử lại sau.", "Please try again later.", "请稍后再试。");
                LblError.IsVisible = true;
                LblError.Text = errMsg;
                return;
            }

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var hetHanStr = json.GetProperty("ngayHetHan").GetString() ?? "";
            Preferences.Set(PrefNgayHetHan, hetHanStr);
            Preferences.Set(PrefDaDungThu, true);
            UpdateTrialButtonState();

            await DisplayAlertAsync(
                AppText.T("Thành công", "Success", "成功"),
                AppText.T(
                    "Bạn đã kích hoạt gói dùng thử 3 ngày.",
                    "Your 3-day free trial has been activated.",
                    "你的 3 天免费试用已激活。"),
                AppText.T("Bắt đầu", "Start", "开始"));

            await ExitSubscriptionGateAsync();
        }
        catch (Exception ex)
        {
            LblError.IsVisible = true;
            LblError.Text = AppConfig.BuildConnectionErrorMessage(ex);
        }
        finally
        {
            SetLoading(false);
            HideApiGuide();
        }
    }

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        BtnDungThu.IsEnabled = !loading && !Preferences.Get(PrefDaDungThu, false);
        BtnMuaNgay.IsEnabled = !loading;
        BtnMuaTuan.IsEnabled = !loading;
        BtnMuaThang.IsEnabled = !loading;
        BtnMuaNam.IsEnabled = !loading;
        BtnScanRecoveryQr.IsEnabled = !loading;
        BtnEnterRecoveryCode.IsEnabled = !loading;
        BtnPasteRecoveryCode.IsEnabled = !loading;
        BtnCopyRecoveryCode.IsEnabled = !loading;
    }

    private async Task ExitSubscriptionGateAsync()
    {
        var rootNavigation = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation ?? Navigation;

        if (rootNavigation.ModalStack.Count > 0)
        {
            await rootNavigation.PopModalAsync();
            return;
        }

        if (Navigation.NavigationStack.LastOrDefault() == this)
        {
            await Navigation.PushAsync(new MainPage(), animated: false);
            Navigation.RemovePage(this);
        }
    }
}
