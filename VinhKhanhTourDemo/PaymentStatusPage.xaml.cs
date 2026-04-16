using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanhTourDemo;

/// <summary>
/// Trang chờ admin duyệt yêu cầu thanh toán.
/// Polling API /api/subscription/request/{id} mỗi 10 giây.
/// Khi được duyệt → lưu ngày hết hạn vào Preferences → hiện thành công.
/// </summary>
public partial class PaymentStatusPage : ContentPage
{

    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    { Timeout = TimeSpan.FromSeconds(10) };

    private readonly string _yeuCauId;
    private readonly string _loaiGoi;
    private CancellationTokenSource? _pollCts;
    private int _countdownSec = 10;

    public PaymentStatusPage(string yeuCauId, string loaiGoi, string noiDung)
    {
        InitializeComponent();
        _yeuCauId = yeuCauId;
        _loaiGoi  = loaiGoi;
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
            if (ct.IsCancellationRequested) break;

            // Đếm ngược 10 giây
            for (_countdownSec = 10; _countdownSec > 0 && !ct.IsCancellationRequested; _countdownSec--)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    LblDemGiay.Text = $"Kiểm tra lại sau {_countdownSec}s…");
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
            if (!res.IsSuccessStatusCode) return;

            var json     = await res.Content.ReadFromJsonAsync<JsonElement>();
            var trangthai = json.GetProperty("trangThai").GetString() ?? "";

            if (trangthai == "da_duyet")
            {
                _pollCts?.Cancel();
                var ngayHetHanStr = json.TryGetProperty("ngayHetHan", out var nh)
                    ? nh.GetString() ?? "" : "";
                MainThread.BeginInvokeOnMainThread(() => ShowSuccess(ngayHetHanStr));
            }
            else if (trangthai == "tu_choi")
            {
                _pollCts?.Cancel();
                var lyDo = json.TryGetProperty("ghiChuAdmin", out var gc) ? gc.GetString() : null;
                MainThread.BeginInvokeOnMainThread(() => ShowRejected(lyDo));
            }
        }
        catch
        {
            // Bỏ qua lỗi mạng — tiếp tục polling
        }
    }

    private void ShowSuccess(string ngayHetHanStr)
    {
        // Lưu ngày hết hạn vào Preferences để app hoạt động offline
        if (!string.IsNullOrEmpty(ngayHetHanStr))
            Preferences.Set("sub_ngay_het_han", ngayHetHanStr);

        ViewChoDuyet.IsVisible = false;
        ViewDaDuyet.IsVisible  = true;

        if (DateTime.TryParse(ngayHetHanStr, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var hetHan))
        {
            var soNgay = Math.Max(1, (int)(hetHan - DateTime.UtcNow).TotalDays + 1);
            LblHetHan.Text = $"Gói của bạn có hiệu lực đến {hetHan.ToLocalTime():dd/MM/yyyy}\n(còn {soNgay} ngày)";
        }
    }

    private void ShowRejected(string? lyDo)
    {
        ViewChoDuyet.IsVisible = false;
        ViewTuChoi.IsVisible   = true;
        LblLyDo.Text = string.IsNullOrEmpty(lyDo)
            ? "Admin không tìm thấy giao dịch khớp. Kiểm tra lại nội dung chuyển khoản."
            : lyDo;
    }

    private async void OnBatDauClicked(object? sender, EventArgs? e)
    {
        // Đóng toàn bộ modal stack (PaymentStatusPage + PaymentPage + SubscriptionPage)
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopToRootAsync(animated: true);
        await Shell.Current.GoToAsync("//MainPage");
    }

    private async void OnThuLaiClicked(object? sender, EventArgs? e)
        => await Navigation.PopModalAsync();   // về PaymentPage

    private async void OnDongClicked(object? sender, EventArgs? e)
    {
        // Đóng cả PaymentPage + PaymentStatusPage
        await Navigation.PopModalAsync();
        await Navigation.PopModalAsync();
    }
}
