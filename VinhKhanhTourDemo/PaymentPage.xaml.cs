using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanhTourDemo;

/// <summary>
/// Trang thanh toán QR — hiển thị mã QR chuyển khoản và chờ người dùng xác nhận.
/// Sau khi người dùng tapping "Đã chuyển khoản", tạo yêu cầu trên server
/// rồi chuyển sang PaymentStatusPage để polling kết quả duyệt.
/// </summary>
public partial class PaymentPage : ContentPage
{

    // Thông tin ngân hàng nhận tiền (cập nhật theo thực tế)
    private const string BANK_ID    = "MB";
    private const string ACCOUNT_NO = "0347491930";
    private const string ACCOUNT_NAME = "VINH KHANH TOUR";

    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly Dictionary<string, (decimal Gia, string Ten, int SoNgay)> GoiInfo = new()
    {
        ["ngay"]  = (29_000m,   "Gói 1 ngày",   1),
        ["tuan"]  = (99_000m,   "Gói 1 tuần",   7),
        ["thang"] = (199_000m,  "Gói 1 tháng",  30),
        ["nam"]   = (999_000m,  "Gói 1 năm",   365),
    };

    private readonly string _loaiGoi;
    private readonly string _deviceId;
    private string _noiDungChuyen = "";

    public PaymentPage(string loaiGoi)
    {
        InitializeComponent();
        _loaiGoi = loaiGoi;
        _deviceId = GetDeviceId();
        SetupUI();
    }

    private static string GetDeviceId()
    {
        var id = Preferences.Get("device_id", "");
        if (!string.IsNullOrEmpty(id)) return id;
        id = Guid.NewGuid().ToString("N");
        Preferences.Set("device_id", id);
        return id;
    }

    private void SetupUI()
    {
        if (!GoiInfo.TryGetValue(_loaiGoi, out var info)) return;

        // Nội dung chuyển khoản: VKT THANG ABCDEF
        var shortId = _deviceId[..Math.Min(6, _deviceId.Length)].ToUpper();
        _noiDungChuyen = $"VKT {_loaiGoi.ToUpper()} {shortId}";

        LblTenGoi.Text  = $"{info.Ten} — {info.SoNgay} ngày sử dụng";
        LblSoTK.Text    = ACCOUNT_NO;
        LblSoTien.Text  = $"{info.Gia:N0}đ";
        LblNoiDung.Text = _noiDungChuyen;

        // VietQR URL
        var encodedDesc = Uri.EscapeDataString(_noiDungChuyen);
        var qrUrl = $"https://img.vietqr.io/image/{BANK_ID}-{ACCOUNT_NO}-compact2.png" +
                    $"?amount={(long)info.Gia}&addInfo={encodedDesc}&accountName={Uri.EscapeDataString(ACCOUNT_NAME)}";
        ImgQR.Source = ImageSource.FromUri(new Uri(qrUrl));
    }

    private async void OnCopyNoiDungClicked(object? sender, EventArgs? e)
    {
        await Clipboard.SetTextAsync(_noiDungChuyen);
        BtnCopyNoiDung.Text = "✅";
        await Task.Delay(1500);
        BtnCopyNoiDung.Text = "📋";
    }

    private async void OnDaChuyenKhoanClicked(object? sender, EventArgs? e)
    {
        SetLoading(true);
        LblError.IsVisible = false;

        try
        {
            var body = new { MaThietBi = _deviceId, LoaiGoi = _loaiGoi };
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var res  = await _http.PostAsJsonAsync($"{apiBaseUrl}/api/subscription/request", body);

            if (!res.IsSuccessStatusCode)
            {
                var errJson = await res.Content.ReadFromJsonAsync<JsonElement>();
                LblError.Text = errJson.TryGetProperty("message", out var m)
                    ? m.GetString() : "Lỗi tạo yêu cầu. Thử lại sau.";
                LblError.IsVisible = true;
                return;
            }

            var json     = await res.Content.ReadFromJsonAsync<JsonElement>();
            var yeuCauId = json.GetProperty("yeuCauId").GetString() ?? "";

            // Chuyển sang trang chờ duyệt
            await Navigation.PushModalAsync(
                new PaymentStatusPage(yeuCauId, _loaiGoi, _noiDungChuyen),
                animated: true);
        }
        catch (Exception ex)
        {
            LblError.IsVisible = true;
            LblError.Text = "Lỗi kết nối: " + ex.Message;
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnHuyClicked(object? sender, EventArgs? e)
        => await Navigation.PopModalAsync();

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning  = loading;
        LoadingIndicator.IsVisible  = loading;
        BtnDaChuyenKhoan.IsEnabled  = !loading;
    }
}
