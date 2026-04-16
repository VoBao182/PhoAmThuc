using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanhTourDemo;

/// <summary>
/// Trang thanh toan QR: hien thi ma QR chuyen khoan va cho nguoi dung xac nhan.
/// Sau khi nguoi dung nhan "Da chuyen khoan", tao yeu cau tren server
/// roi chuyen sang PaymentStatusPage de polling ket qua duyet.
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
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly Dictionary<string, (decimal Gia, string Ten, int SoNgay)> GoiInfo = new()
    {
        ["ngay"] = (29_000m, "Goi 1 ngay", 1),
        ["tuan"] = (99_000m, "Goi 1 tuan", 7),
        ["thang"] = (199_000m, "Goi 1 thang", 30),
        ["nam"] = (999_000m, "Goi 1 nam", 365),
    };

    private readonly string _loaiGoi;
    private readonly string _deviceId;
    private string _noiDungChuyen = "";

    public PaymentPage(string loaiGoi)
    {
        InitializeComponent();
        _loaiGoi = loaiGoi;
        _deviceId = GetDeviceId();
        SetupUi();
    }

    private static string GetDeviceId()
    {
        var id = Preferences.Get("device_id", "");
        if (!string.IsNullOrEmpty(id))
            return id;

        id = Guid.NewGuid().ToString("N");
        Preferences.Set("device_id", id);
        return id;
    }

    private void SetupUi()
    {
        if (!GoiInfo.TryGetValue(_loaiGoi, out var info))
            return;

        var shortId = _deviceId[..Math.Min(6, _deviceId.Length)].ToUpperInvariant();
        _noiDungChuyen = $"VKT {_loaiGoi.ToUpperInvariant()} {shortId}";

        LblTenGoi.Text = $"{info.Ten} - {info.SoNgay} ngay su dung";
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
                    : "Loi tao yeu cau. Thu lai sau.";
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
