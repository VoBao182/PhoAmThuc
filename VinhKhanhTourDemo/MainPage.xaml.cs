using Microsoft.Maui.Platform;

namespace VinhKhanhTourDemo;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        // Gọi hàm tải bản đồ Google Maps ngay khi App vừa mở lên
        LoadMap();
    }

    // --- HÀM TẢI BẢN ĐỒ BẰNG IFRAME (CHỐNG LỖI CHẶN APP) ---
    private async void LoadMap()
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Medium);
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location != null)
            {
                // ÉP KIỂU SỐ CHUẨN QUỐC TẾ (Luôn dùng dấu chấm cho số thập phân)
                string latStr = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string lngStr = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

                var htmlSource = new HtmlWebViewSource();
                htmlSource.Html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
                    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
                    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
                    <style>
                        body {{ margin: 0; padding: 0; overflow: hidden; }}
                        #map {{ width: 100vw; height: 100vh; background-color: #e5e5e5; }}
                    </style>
                </head>
                <body>
                    <div id='map'></div>
                    <script>
                        // Dùng tọa độ đã chuẩn hóa
                        var map = L.map('map').setView([{latStr}, {lngStr}], 16);

                        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                            attribution: '© OpenStreetMap'
                        }}).addTo(map);

                        var userMarker = L.marker([{latStr}, {lngStr}]).addTo(map)
                            .bindPopup('<b>📍 Vị trí của bạn</b>').openPopup();

                        var ocOanhMarker = L.marker([10.758955, 106.701831]).addTo(map)
                            .bindPopup('Quán Ốc Oanh (Cách bạn 20m)');
                    </script>
                </body>
                </html>";

                MapWebView.Source = htmlSource;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi GPS", "Không thể lấy vị trí, vui lòng bật GPS.", "OK");
        }
    }
    // --- HÀM ĐỌC GIỌNG NÓI ---
    private async void OnListenClicked(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            btn.Text = "🔊 Đang phát...";
            btn.BackgroundColor = Colors.DarkOrange;
            await TextToSpeech.Default.SpeakAsync("Chào mừng bạn đến với Quán Ốc Oanh. Một trong những địa điểm ẩm thực sôi động nhất phố Vĩnh Khánh.");
            btn.Text = "🎧 Nghe Thuyết Minh";
            btn.BackgroundColor = Color.FromArgb("#4CAF50");
        }
    }

    // --- HÀM CHUYỂN TAB KHÁM PHÁ ---
    private void OnTabKhamPhaTapped(object sender, TappedEventArgs e)
    {
        ViewKhamPha.IsVisible = true;
        ViewBanDo.IsVisible = false;

        BtnKhamPha.Opacity = 1.0;
        LblKhamPha.TextColor = Color.FromArgb("#FF5722");

        BtnBanDo.Opacity = 0.4;
        LblBanDo.TextColor = Colors.Black;
    }

    // --- HÀM CHUYỂN TAB BẢN ĐỒ ---
    private void OnTabBanDoTapped(object sender, TappedEventArgs e)
    {
        ViewKhamPha.IsVisible = false;
        ViewBanDo.IsVisible = true;

        BtnBanDo.Opacity = 1.0;
        LblBanDo.TextColor = Color.FromArgb("#FF5722");

        BtnKhamPha.Opacity = 0.4;
        LblKhamPha.TextColor = Colors.Black;
    }
}