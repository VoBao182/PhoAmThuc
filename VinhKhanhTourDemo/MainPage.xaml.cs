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
    // Thay thế hàm LoadMap cũ bằng hàm này
    private async void LoadMap()
    {
        try
        {
            // 1. LẤY TỌA ĐỘ GPS THẬT CỦA ĐIỆN THOẠI ẢO
            var request = new GeolocationRequest(GeolocationAccuracy.Medium);
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location != null)
            {
                double lat = location.Latitude;
                double lng = location.Longitude;

                // 2. NHÚNG BẢN ĐỒ LEAFLET VÀ VẼ MARKER TỪ TỌA ĐỘ VỪA LẤY
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
                        #map {{ width: 100vw; height: 100vh; }}
                    </style>
                </head>
                <body>
                    <div id='map'></div>
                    <script>
                        // Khởi tạo bản đồ ngay tại vị trí GPS của bạn
                        var map = L.map('map').setView([{lat}, {lng}], 16);

                        // Load gạch bản đồ từ OpenStreetMap (Miễn phí 100%, không lo API Key)
                        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                            attribution: '© OpenStreetMap'
                        }}).addTo(map);

                        // Đánh dấu VỊ TRÍ CỦA BẠN (Lấy từ điện thoại)
                        var userMarker = L.marker([{lat}, {lng}]).addTo(map)
                            .bindPopup('<b>📍 Vị trí của bạn</b>').openPopup();

                        // Đánh dấu QUÁN ỐC OANH (Dữ liệu tĩnh)
                        var ocOanhMarker = L.marker([10.758955, 106.701831]).addTo(map)
                            .bindPopup('Quán Ốc Oanh (Cách bạn 20m)');
                    </script>
                </body>
                </html>";

                // Bơm thẳng lên WebView
                MapWebView.Source = htmlSource;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi GPS", "Không thể lấy vị trí, vui lòng kiểm tra lại.", "OK");
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