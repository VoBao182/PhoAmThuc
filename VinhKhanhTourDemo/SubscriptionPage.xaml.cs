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
    { Timeout = TimeSpan.FromSeconds(15) };

    private const string PREF_DEVICE_ID    = "device_id";
    private const string PREF_NGAY_HET_HAN = "sub_ngay_het_han";
    private const string PREF_DA_DUNG_THU  = "da_dung_thu";

    public SubscriptionPage(bool hetHan = false)
    {
        InitializeComponent();
        BannerHetHan.IsVisible = hetHan;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Ẩn nút dùng thử nếu thiết bị đã từng dùng (lưu local để không cần gọi API)
        bool daDungThu = Preferences.Get(PREF_DA_DUNG_THU, false);
        if (daDungThu)
        {
            BtnDungThu.IsEnabled   = false;
            BtnDungThu.Text        = "Đã sử dụng";
            BtnDungThu.BackgroundColor = Color.FromArgb("#9CA3AF");
        }
    }

    // Lấy (hoặc tạo) mã thiết bị — UUID lưu vĩnh viễn
    private static string GetDeviceId()
    {
        var id = Preferences.Get(PREF_DEVICE_ID, "");
        if (!string.IsNullOrEmpty(id)) return id;
        id = Guid.NewGuid().ToString("N");
        Preferences.Set(PREF_DEVICE_ID, id);
        return id;
    }

    private async void OnMuaGoiClicked(object? sender, EventArgs? e)
    {
        if (sender is not Button btn) return;
        var loaiGoi  = btn.CommandParameter?.ToString() ?? "thang";
        var deviceId = GetDeviceId();

        // Gói dùng thử: kích hoạt trực tiếp (miễn phí, không cần chuyển khoản)
        if (loaiGoi == "thu")
        {
            await ActivateFreeTrialAsync(deviceId);
            return;
        }

        // Gói trả phí: chuyển sang trang thanh toán QR
        await Navigation.PushModalAsync(new PaymentPage(loaiGoi), animated: true);
    }

    private async Task ActivateFreeTrialAsync(string deviceId)
    {
        SetLoading(true);
        LblError.IsVisible = false;

        try
        {
            var body = new { MaThietBi = deviceId, LoaiGoi = "thu" };
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var res  = await _http.PostAsJsonAsync($"{apiBaseUrl}/api/subscription/purchase", body);

            if (!res.IsSuccessStatusCode)
            {
                var errJson = await res.Content.ReadFromJsonAsync<JsonElement>();
                var errMsg  = errJson.TryGetProperty("message", out var m) ? m.GetString() : "Thử lại sau.";
                LblError.IsVisible = true;
                LblError.Text = errMsg;
                return;
            }

            var json      = await res.Content.ReadFromJsonAsync<JsonElement>();
            var hetHanStr = json.GetProperty("ngayHetHan").GetString() ?? "";
            Preferences.Set(PREF_NGAY_HET_HAN, hetHanStr);
            Preferences.Set(PREF_DA_DUNG_THU, true);

            await DisplayAlertAsync("Thành công! 🎉",
                "Bạn đã kích hoạt gói dùng thử 3 ngày.\nChúc bạn khám phá vui vẻ!",
                "Bắt đầu");

            await Navigation.PopModalAsync();
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

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        BtnDungThu.IsEnabled       = !loading && !Preferences.Get(PREF_DA_DUNG_THU, false);
        BtnMuaNgay.IsEnabled       = !loading;
        BtnMuaTuan.IsEnabled       = !loading;
        BtnMuaThang.IsEnabled      = !loading;
        BtnMuaNam.IsEnabled        = !loading;
    }
}
