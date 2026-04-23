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
        BannerHetHan.IsVisible = hetHan;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateRecoveryCard();
        UpdateTrialButtonState();
        HideApiGuide();
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
        BtnDungThu.Text = daDungThu ? "Đã sử dụng" : "Thử ngay";
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
        LblRecoveryStatus.Text = "Đã copy mã khôi phục hiện tại.";
    }

    private async void OnPasteRecoveryCodeClicked(object? sender, EventArgs e)
    {
        var code = await Clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(code))
        {
            LblRecoveryStatus.Text = "Clipboard chưa có mã khôi phục.";
            return;
        }

        await RestoreFromRecoveryCodeAsync(code);
    }

    private async void OnEnterRecoveryCodeClicked(object? sender, EventArgs e)
    {
        var code = await DisplayPromptAsync(
            "Khôi phục dữ liệu",
            "Nhập mã khôi phục hoặc nội dung quét từ QR.",
            "Khôi phục",
            "Hủy",
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
            LblRecoveryStatus.Text = "Cần cấp quyền camera để quét QR.";
            return;
        }

        await Navigation.PushModalAsync(new QrScannerPage(RestoreFromRecoveryCodeAsync), animated: true);
    }

    private async Task RestoreFromRecoveryCodeAsync(string rawCode)
    {
        if (!DeviceIdentity.TrySetDeviceIdOverride(rawCode, out var restoredDeviceId))
        {
            LblRecoveryStatus.Text = "Mã khôi phục không hợp lệ.";
            return;
        }

        UpdateRecoveryCard();
        SetLoading(true);
        LblError.IsVisible = false;
        LblRecoveryStatus.Text = "Đã nhận mã cũ. Đang đồng bộ gói...";

        try
        {
            var restored = await RestoreSubscriptionStateAsync(restoredDeviceId);
            UpdateTrialButtonState();

            if (restored)
            {
                await DisplayAlertAsync(
                    "Đã khôi phục",
                    "Ứng dụng đã đồng bộ lại gói sử dụng và mã thiết bị cũ.",
                    "Tiếp tục");

                await ExitSubscriptionGateAsync();
                return;
            }

            LblRecoveryStatus.Text = "Đã đổi sang mã cũ, nhưng mã này chưa có gói còn hạn. Bạn có thể mua/dùng thử bằng mã này.";
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
                    : "Thử lại sau.";
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
                "Thành công",
                "Bạn đã kích hoạt gói dùng thử 3 ngày.",
                "Bắt đầu");

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
