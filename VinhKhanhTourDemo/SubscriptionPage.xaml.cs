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
        UpdateTrialButtonState();
        HideApiGuide();
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
