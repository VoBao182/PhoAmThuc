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
        Timeout = TimeSpan.FromSeconds(15)
    };

    private const string PrefDeviceId = "device_id";
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
        UpdateTrialButtonState();
        UpdateApiGuide();
    }

    private void UpdateTrialButtonState()
    {
        bool daDungThu = Preferences.Get(PrefDaDungThu, false);
        BtnDungThu.IsEnabled = !daDungThu;
        BtnDungThu.Text = daDungThu ? "Da su dung" : "Thu ngay";
        BtnDungThu.BackgroundColor = daDungThu
            ? Color.FromArgb("#9CA3AF")
            : Color.FromArgb("#22C55E");
    }

    private static string GetDeviceId()
    {
        var id = Preferences.Get(PrefDeviceId, "");
        if (!string.IsNullOrEmpty(id))
            return id;

        id = Guid.NewGuid().ToString("N");
        Preferences.Set(PrefDeviceId, id);
        return id;
    }

    private async void OnMuaGoiClicked(object? sender, EventArgs? e)
    {
        if (sender is not Button btn)
            return;

        var loaiGoi = btn.CommandParameter?.ToString() ?? "thang";
        var deviceId = GetDeviceId();

        if (loaiGoi == "thu")
        {
            await ActivateFreeTrialAsync(deviceId);
            return;
        }

        await Navigation.PushModalAsync(new PaymentPage(loaiGoi), animated: true);
    }

    private void UpdateApiGuide()
    {
        ApiGuideCard.IsVisible = !AppConfig.HasConfiguredHostedApiBaseUrl;
        if (!ApiGuideCard.IsVisible)
            return;

        LblApiGuideText.Text = DeviceInfo.Platform == DevicePlatform.Android
            ? $"Neu dang test tren dien thoai qua USB, {AppConfig.ApiBaseUrl} chi dung khi da bat adb reverse tcp:{AppEndpointOptions.ApiPort} tcp:{AppEndpointOptions.ApiPort}. Neu khong, hay nhap IP may tinh hoac public API URL."
            : $"Hay nhap public API URL hoac URL backend dang chay, vi du http://localhost:{AppEndpointOptions.ApiPort}.";
        LblApiGuideStatus.Text = $"Dang uu tien: {AppConfig.ApiBaseUrl}";
    }

    private async void OnConfigureApiClicked(object? sender, EventArgs e)
    {
        var apiBaseUrl = await ApiConnectionPrompt.PromptForApiBaseUrlAsync(this, _http);
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            LblError.IsVisible = false;
            UpdateApiGuide();
        }
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
                    : "Thu lai sau.";
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
                "Thanh cong",
                "Ban da kich hoat goi dung thu 3 ngay.",
                "Bat dau");

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            LblError.IsVisible = true;
            LblError.Text = AppConfig.BuildConnectionErrorMessage(ex);
        }
        finally
        {
            SetLoading(false);
            UpdateApiGuide();
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
    }
}
