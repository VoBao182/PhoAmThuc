using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanhTourDemo;

/// <summary>
/// Trang chờ admin duyệt yêu cầu thanh toán.
/// Polling API /api/subscription/request/{id} moi 10 giay.
/// Khi được duyệt thì lưu ngày hết hạn vào Preferences và hiện thành công.
/// </summary>
public partial class PaymentStatusPage : ContentPage
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    {
        Timeout = AppConfig.PreferredApiRequestTimeout
    };

    private readonly string _yeuCauId;
    private readonly string _loaiGoi;
    private CancellationTokenSource? _pollCts;
    private int _countdownSec = 10;

    public PaymentStatusPage(string yeuCauId, string loaiGoi, string noiDung)
    {
        InitializeComponent();
        _yeuCauId = yeuCauId;
        _loaiGoi = loaiGoi;
        LblNoiDungRef.Text = noiDung;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _pollCts = new CancellationTokenSource();
        _ = StartPollingAsync(_pollCts.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pollCts?.Cancel();
    }

    private async Task StartPollingAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await PollStatusAsync();
            if (ct.IsCancellationRequested)
                break;

            for (_countdownSec = 10; _countdownSec > 0 && !ct.IsCancellationRequested; _countdownSec--)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    LblDemGiay.Text = $"Kiểm tra lại sau {_countdownSec}s...");
                await Task.Delay(1000, ct).ContinueWith(_ => { });
            }
        }
    }

    private async Task PollStatusAsync()
    {
        try
        {
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var res = await _http.GetAsync($"{apiBaseUrl}/api/subscription/request/{_yeuCauId}");
            if (!res.IsSuccessStatusCode)
                return;

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var trangThai = json.GetProperty("trangThai").GetString() ?? "";

            if (trangThai == "da_duyet")
            {
                _pollCts?.Cancel();
                var ngayHetHanStr = json.TryGetProperty("ngayHetHan", out var nh)
                    ? nh.GetString() ?? ""
                    : "";
                MainThread.BeginInvokeOnMainThread(() => ShowSuccess(ngayHetHanStr));
            }
            else if (trangThai == "tu_choi")
            {
                _pollCts?.Cancel();
                var lyDo = json.TryGetProperty("ghiChuAdmin", out var gc)
                    ? gc.GetString()
                    : null;
                MainThread.BeginInvokeOnMainThread(() => ShowRejected(lyDo));
            }
        }
        catch
        {
            // Bo qua loi mang va tiep tuc polling.
        }
    }

    private void ShowSuccess(string ngayHetHanStr)
    {
        if (!string.IsNullOrEmpty(ngayHetHanStr))
            Preferences.Set("sub_ngay_het_han", ngayHetHanStr);

        ViewChoDuyet.IsVisible = false;
        ViewDaDuyet.IsVisible = true;

        if (DateTime.TryParse(
                ngayHetHanStr,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var hetHan))
        {
            var soNgay = Math.Max(1, (int)(hetHan - DateTime.UtcNow).TotalDays + 1);
            LblHetHan.Text = $"Gói của bạn có hiệu lực đến {hetHan.ToLocalTime():dd/MM/yyyy}\n(còn {soNgay} ngày)";
        }
    }

    private void ShowRejected(string? lyDo)
    {
        ViewChoDuyet.IsVisible = false;
        ViewTuChoi.IsVisible = true;
        LblLyDo.Text = string.IsNullOrEmpty(lyDo)
            ? "Admin không tìm thấy giao dịch khớp. Kiểm tra lại nội dung chuyển khoản."
            : lyDo;
    }

    private async void OnBatDauClicked(object? sender, EventArgs? e)
    {
        await ClosePaymentFlowAsync(closeSubscriptionPage: true);
    }

    private async void OnThuLaiClicked(object? sender, EventArgs? e)
    {
        var navigation = GetRootNavigation();
        if (navigation.ModalStack.Count > 0)
            await navigation.PopModalAsync();
    }

    private async void OnDongClicked(object? sender, EventArgs? e)
    {
        await ClosePaymentFlowAsync(closeSubscriptionPage: false);
    }

    private async Task ClosePaymentFlowAsync(bool closeSubscriptionPage)
    {
        _pollCts?.Cancel();

        var navigation = GetRootNavigation();
        var modalsToLeave = closeSubscriptionPage
            ? 0
            : navigation.ModalStack.FirstOrDefault() is SubscriptionPage ? 1 : 0;

        while (navigation.ModalStack.Count > modalsToLeave)
            await navigation.PopModalAsync();

        if (closeSubscriptionPage && navigation.NavigationStack.LastOrDefault() is SubscriptionPage subscriptionPage)
        {
            await navigation.PushAsync(new MainPage(), animated: false);
            navigation.RemovePage(subscriptionPage);
            return;
        }

        if (navigation.NavigationStack.Count > 1)
            await navigation.PopToRootAsync(animated: false);
    }

    private INavigation GetRootNavigation()
        => Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation ?? Navigation;
}
