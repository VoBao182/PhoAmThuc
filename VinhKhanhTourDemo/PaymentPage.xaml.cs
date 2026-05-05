using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanhTourDemo;

/// <summary>
/// Trang thanh toán QR: hiển thị mã QR chuyển khoản và chờ người dùng xác nhận.
/// Sau khi người dùng nhấn "Đã chuyển khoản", tạo yêu cầu trên server
/// rồi chuyển sang PaymentStatusPage để polling kết quả duyệt.
/// </summary>
public partial class PaymentPage : ContentPage
{
    private const string BankId = "MB";
    private const string AccountNo = "0347491930";
    private const string AccountName = "VINH KHANH TOUR";

    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    {
        Timeout = AppConfig.PreferredApiRequestTimeout
    };

    private static readonly Dictionary<string, (decimal Gia, string Ten, int SoNgay)> GoiInfo = new()
    {
        ["ngay"] = (29_000m, "Gói 1 ngày", 1),
        ["tuan"] = (99_000m, "Gói 1 tuần", 7),
        ["thang"] = (199_000m, "Gói 1 tháng", 30),
        ["nam"] = (999_000m, "Gói 1 năm", 365),
    };

    private readonly string _loaiGoi;
    private readonly string _deviceId;
    private string _noiDungChuyen = "";

    public PaymentPage(string loaiGoi)
    {
        InitializeComponent();
        _loaiGoi = loaiGoi;
        _deviceId = DeviceIdentity.GetDeviceId();
        ApplyLocalizedUiText();
        SetupUi();
        HideApiGuide();
    }

    private void ApplyLocalizedUiText()
    {
        LblPaymentTitle.Text = AppText.T("Thanh toán chuyển khoản", "Bank transfer payment", "银行转账支付");
        LblQrInstruction.Text = AppText.T("Quét mã QR để chuyển khoản", "Scan the QR code to transfer", "扫描 QR 码转账");
        LblBankCaption.Text = AppText.T("Ngân hàng", "Bank", "银行");
        LblAccountCaption.Text = AppText.T("Số tài khoản", "Account number", "账号");
        LblAmountCaption.Text = AppText.T("Số tiền", "Amount", "金额");
        LblTransferContentCaption.Text = AppText.T("Nội dung CK", "Transfer note", "转账备注");
        LblTransferNote.Text = AppText.T(
            "Nhập đúng nội dung chuyển khoản để hệ thống xác nhận tự động",
            "Use the exact transfer note so the system can verify automatically",
            "请填写准确的转账备注，以便系统自动确认");
        LblApiGuideTitle.Text = AppText.T(
            "Cần kết nối API để gửi yêu cầu thanh toán",
            "API connection is required to send the payment request",
            "需要连接 API 才能提交支付请求");
        BtnConfigureApi.Text = AppText.T("Nhập API URL", "Enter API URL", "输入 API URL");
        BtnDaChuyenKhoan.Text = AppText.T(
            "Tôi đã chuyển khoản",
            "I have transferred",
            "我已转账");
        BtnCancel.Text = AppText.T("Hủy", "Cancel", "取消");
    }

    private void SetupUi()
    {
        if (!GoiInfo.TryGetValue(_loaiGoi, out var info))
            return;

        var shortId = _deviceId[..Math.Min(6, _deviceId.Length)].ToUpperInvariant();
        _noiDungChuyen = $"VKT {_loaiGoi.ToUpperInvariant()} {shortId}";

        LblTenGoi.Text = $"{AppText.PlanName(_loaiGoi)} - {AppText.PlanUsageDays(info.SoNgay)}";
        LblSoTK.Text = AccountNo;
        LblSoTien.Text = $"{info.Gia:N0}d";
        LblNoiDung.Text = _noiDungChuyen;

        var encodedDesc = Uri.EscapeDataString(_noiDungChuyen);
        var qrUrl = $"https://img.vietqr.io/image/{BankId}-{AccountNo}-compact2.png" +
                    $"?amount={(long)info.Gia}&addInfo={encodedDesc}&accountName={Uri.EscapeDataString(AccountName)}";
        ImgQR.Source = ImageSource.FromUri(new Uri(qrUrl));
    }

    private async void OnCopyNoiDungClicked(object? sender, EventArgs? e)
    {
        await Clipboard.SetTextAsync(_noiDungChuyen);
        BtnCopyNoiDung.Text = "OK";
        await Task.Delay(1500);
        BtnCopyNoiDung.Text = "Copy";
    }

    private async void OnDaChuyenKhoanClicked(object? sender, EventArgs? e)
    {
        SetLoading(true);
        LblError.IsVisible = false;

        try
        {
            var body = new { MaThietBi = _deviceId, LoaiGoi = _loaiGoi };
            var apiBaseUrl = await ApiConnectionPrompt.EnsureConnectedApiBaseUrlAsync(this, _http);
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                LblError.Text = AppConfig.BuildConnectionErrorMessage(
                    new HttpRequestException("Unable to reach API."));
                LblError.IsVisible = true;
                return;
            }

            var res = await _http.PostAsJsonAsync($"{apiBaseUrl}/api/subscription/request", body);

            if (!res.IsSuccessStatusCode)
            {
                var errJson = await res.Content.ReadFromJsonAsync<JsonElement>();
                LblError.Text = errJson.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : AppText.T(
                        "Lỗi tạo yêu cầu. Thử lại sau.",
                        "Could not create the request. Please try again later.",
                        "无法创建请求。请稍后再试。");
                LblError.IsVisible = true;
                return;
            }

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var yeuCauId = json.GetProperty("yeuCauId").GetString() ?? "";

            await Navigation.PushModalAsync(
                new PaymentStatusPage(yeuCauId, _loaiGoi, _noiDungChuyen),
                animated: true);
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

    private async void OnHuyClicked(object? sender, EventArgs? e)
        => await Navigation.PopModalAsync();

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        BtnDaChuyenKhoan.IsEnabled = !loading;
    }
}
