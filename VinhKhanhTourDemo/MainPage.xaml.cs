using Microsoft.Maui.Controls.Shapes;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Threading;

namespace VinhKhanhTourDemo;

public class PoiDto
{
    public Guid Id { get; set; }
    public string TenPOI { get; set; } = "";
    public double KinhDo { get; set; }
    public double ViDo { get; set; }
    public int BanKinh { get; set; }
    public int MucUuTien { get; set; }
    public string? AnhDaiDien { get; set; }
    public string? DiaChi { get; set; }
    public string? SDT { get; set; }
}

public partial class MainPage : ContentPage
{
    private const int COOLDOWN_MINUTES = 10;
    private const string ViewedPoiIdsPreferenceKey = "viewed_poi_ids";
    private const string VisitedPoiIdsPreferenceKey = "visited_poi_ids";

    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private List<PoiDto> _pois = [];
    private Location? _userLocation;
    private PoiDto? _currentPoi;
    private PoiDto? _playingPoi;
    private PoiDto? _sheetPoi;
    private CancellationTokenSource? _gpsCts;
    private bool _isInitialized;
    private bool _isMapReady;
    private Location? _pendingMapLocation;
    private Guid? _pendingHighlightPoiId;
    private string _searchText = "";
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private readonly SemaphoreSlim _historySyncLock = new(1, 1);
    private readonly Dictionary<string, DateTime> _lastSpokenTime = new();
    private int _heartbeatTick = 0;
    private int _poiRefreshTick = 0;
    private bool _subscriptionModalOpen;
    private bool _isUsingFallbackData;
    private string? _lastDataLoadError;
    private const int HEARTBEAT_EVERY_TICKS   = 3;   // mỗi 3 lần GPS poll = 15 giây
    private const int POI_REFRESH_EVERY_TICKS = 6;   // mỗi 6 lần GPS poll = 30 giây (đủ nhanh để test)

    public MainPage()
    {
        InitializeComponent();
        ApplyLocalizedUiText();
        UpdateCaiDatUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Kiểm tra gói đăng ký — nếu chưa có hoặc hết hạn → hiện modal mua gói
        if (await EnsureSubscriptionGateAsync())
            return;

        if (!_isInitialized)
        {
            _isInitialized = true;
            await LoadPoisFromApi();
        }

        await EnsureGpsTrackingAsync();
        _ = SyncPoiHistoryAsync();
    }

    private async Task<bool> EnsureSubscriptionGateAsync()
    {
        if (KiemTraSubscription())
            return false;

        if (_subscriptionModalOpen)
            return true;

        _subscriptionModalOpen = true;
        try
        {
            bool hetHan = Preferences.ContainsKey("sub_ngay_het_han");
            await Task.Yield();
            await Navigation.PushModalAsync(new SubscriptionPage(hetHan), animated: false);
            return true;
        }
        finally
        {
            _subscriptionModalOpen = false;
        }
    }

    private static bool KiemTraSubscription()
    {
        var hetHanStr = Preferences.Get("sub_ngay_het_han", "");
        if (string.IsNullOrEmpty(hetHanStr)) return false;
        if (!DateTime.TryParse(hetHanStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var hetHan))
            return false;
        return hetHan > DateTime.UtcNow;
    }

    private static HashSet<string> GetSavedPoiIds(string preferenceKey)
    {
        var raw = Preferences.Get(preferenceKey, "");
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void SavePoiIds(string preferenceKey, HashSet<string> poiIds)
    {
        Preferences.Set(preferenceKey, string.Join('|', poiIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)));
    }

    private static int GetSavedPoiCount(string preferenceKey) => GetSavedPoiIds(preferenceKey).Count;

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value
            .Trim()
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private static bool MatchesSearch(string? source, string normalizedKeyword)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrEmpty(normalizedKeyword))
            return false;

        return NormalizeSearchText(source).Contains(normalizedKeyword, StringComparison.Ordinal);
    }

    private static void RecordSavedPoi(string preferenceKey, PoiDto poi)
    {
        var poiIds = GetSavedPoiIds(preferenceKey);
        if (poiIds.Add(poi.Id.ToString()))
            SavePoiIds(preferenceKey, poiIds);
    }

    private void RecordViewedPoi(PoiDto poi) => RecordSavedPoi(ViewedPoiIdsPreferenceKey, poi);

    private void RecordVisitedPoi(PoiDto poi) => RecordSavedPoi(VisitedPoiIdsPreferenceKey, poi);

    private async Task OpenPoiDetailAsync(PoiDto poi)
    {
        RecordViewedPoi(poi);
        _ = RecordPoiViewAsync(poi);   // ghi nhận lên server để CMS thấy số quán đã xem
        _ = SyncPoiHistoryAsync();
        UpdateCaiDatUI();
        await Navigation.PushAsync(new DetailPage(poi));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _gpsCts?.Cancel();
    }

    private string GetText(string vi, string en, string zh)
    {
        string lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        return lang switch
        {
            "en" => en,
            "zh" => zh,
            _ => vi
        };
    }

    private void ApplyLocalizedUiText()
    {
        LblHeaderTitle.Text = "Vinh Khanh Tour";
        LblHeaderSub.Text = GetText("Khám phá phố ẩm thực", "Discover the food street", "探索美食街");
        NowPlayingTitle.Text = GetText("Đang phát thuyết minh", "Now playing audio guide", "正在播放语音导览");
        BtnNowPlayingDetail.Text = GetText("Chi tiết", "Details", "详情");
        SearchEntry.Placeholder = GetText("Tìm quán ăn, địa chỉ...", "Search places, address...", "搜索店名、地址...");
        LblNearbyTitle.Text = GetText("Điểm đến gần bạn", "Nearby places", "附近地点");
        LblSheetNearBadge.Text = GetText("Đang trong vùng audio", "Inside audio zone", "已进入语音范围");
        SheetBtnDetail.Text = GetText("Xem chi tiết", "View details", "查看详情");
        SheetBtnMap.Text = GetText("Chỉ đường", "Directions", "导航");
        BtnSheetClose.Text = GetText("Đóng", "Close", "关闭");
        LblKhamPha.Text = GetText("Khám phá", "Explore", "探索");
        LblBanDo.Text = GetText("Bản đồ", "Map", "地图");
        LblCaiDat.Text = GetText("Cài đặt", "Settings", "设置");
        BtnReloadData.Text = GetText("Tải lại dữ liệu", "Reload data", "重新加载数据");
        LblModeCaption.Text = GetText("Chế độ", "Mode", "模式");
        LblSubCaption.Text = GetText("Gói đăng ký", "Subscription", "订阅套餐");
        LblLocalDataCaption.Text = GetText("Mã thiết bị", "Device ID", "设备编号");
        LblLanguageCaption.Text = GetText("Ngôn ngữ thuyết minh", "Audio language", "语音语言");
        BtnGiaHan.Text = GetText("Gia hạn gói", "Renew plan", "续费套餐");
        LblApiCaption.Text = GetText("Ket noi API", "API connection", "API lian jie");
        LblApiHint.Text = DeviceInfo.Platform == DevicePlatform.Android
            ? GetText(
                "Emulator: http://10.0.2.2:5118. May that: nhap IP may tinh, vi du http://192.168.1.5:5118.",
                "Emulator: http://10.0.2.2:5118. Physical device: enter your computer IP, for example http://192.168.1.5:5118.",
                "Mo ni qi: http://10.0.2.2:5118. Zhen ji: qing shu ru dian nao IP, li ru http://192.168.1.5:5118.")
            : GetText(
                "Mac dinh: http://localhost:5118",
                "Default: http://localhost:5118",
                "Mo ren: http://localhost:5118");
        EntryApiBaseUrl.Placeholder = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://192.168.1.5:5118"
            : "http://localhost:5118";
        BtnSaveApiUrl.Text = GetText("Luu API URL", "Save API URL", "Bao cun API URL");
        BtnResetApiUrl.Text = GetText("Dung mac dinh", "Use default", "Shi yong mo ren zhi");
    }

    private async Task EnsureGpsTrackingAsync()
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (permission != PermissionStatus.Granted)
            permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (permission != PermissionStatus.Granted)
        {
            GpsStatusDot.BackgroundColor = Color.FromArgb("#AAAAAA");
                await DisplayAlertAsync(
                GetText("Can cap vi tri", "Location required", "需要位置权限"),
                GetText(
                    "Hay cap quyen vi tri de ban do xac dinh vi tri hien tai va tu dong phat audio.",
                    "Please allow location access so the map can detect your current position and trigger audio automatically.",
                    "请允许位置权限，以便地图获取当前位置并自动播放音频。"),
                GetText("OK", "OK", "确定"));
            return;
        }

        StartGpsTracking();
    }

    private async Task LoadPoisFromApi(bool showFailureAlert = false)
    {
        try
        {
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var result = await _http.GetFromJsonAsync<List<PoiDto>>($"{apiBaseUrl}/api/poi");
            if (result != null)
            {
                _pois = result;
                _isUsingFallbackData = false;
                _lastDataLoadError = null;
                Console.WriteLine($"=== Loaded {_pois.Count} POI ===");
                foreach (var p in _pois)
                    Console.WriteLine($"  - {p.TenPOI}: {p.ViDo}, {p.KinhDo}");

                _ = SyncPoiHistoryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API error, fallback to local sample: {ex.Message}");
            _pois = CreateFallbackPois();
            _isUsingFallbackData = true;
            _lastDataLoadError = ex.Message;

            if (showFailureAlert)
            {
                await DisplayAlertAsync(
                GetText("Không tải được dữ liệu", "Unable to load data", "无法加载数据"),
                GetText(
                    "API hoặc database đang lỗi. Ứng dụng tạm dùng dữ liệu mẫu để bạn tiếp tục kiểm tra giao diện.",
                    "The API or database is failing. The app is temporarily using sample data so you can continue testing the UI.",
                    "API 或数据库发生错误。应用暂时使用示例数据，方便你继续测试界面。"),
                "OK");
            }
        }

        LoadMap();
        RenderPoiCards();
        UpdateApiConnectionUi();
    }

    private string BuildApiFailureMessage()
    {
        var apiBaseUrl = AppConfig.ApiBaseUrl;

        return DeviceInfo.Platform == DevicePlatform.Android
            ? GetText(
                $"Khong ket noi duoc toi {apiBaseUrl}. Neu ban dang chay tren may that, hay vao Cai dat > API URL va nhap IP may tinh. Ung dung tam dung du lieu mau.",
                $"Cannot reach {apiBaseUrl}. If you are using a physical Android device, open Settings > API URL and enter your computer IP. The app is using sample data for now.",
                $"Wu fa lian jie dao {apiBaseUrl}. Ruo guo ni zheng zai shi yong Android zhen ji, qing zai she zhi > API URL zhong shu ru dian nao IP. Ying yong zan shi shi yong shi li shu ju.")
            : GetText(
                $"Khong ket noi duoc toi {apiBaseUrl}. Hay kiem tra backend dang chay o cong 5118. Ung dung tam dung du lieu mau.",
                $"Cannot reach {apiBaseUrl}. Make sure the backend is running on port 5118. The app is using sample data for now.",
                $"Wu fa lian jie dao {apiBaseUrl}. Qing que ren hou duan yi zai 5118 duan kou yun han. Ying yong zan shi shi yong shi li shu ju.");
    }

    private void UpdateApiConnectionUi()
    {
        if (EntryApiBaseUrl is not null && !EntryApiBaseUrl.IsFocused)
            EntryApiBaseUrl.Text = AppConfig.CustomApiBaseUrl ?? "";

        var apiBaseUrl = AppConfig.ApiBaseUrl;
        var statusText = GetText($"Dang dung: {apiBaseUrl}", $"Current URL: {apiBaseUrl}", $"Dang qian URL: {apiBaseUrl}");

        if (_isUsingFallbackData)
        {
            statusText += GetText(" • Du lieu mau", " • Sample data", " • Shi li shu ju");
            LblApiStatus.TextColor = Color.FromArgb("#DC2626");
        }
        else
        {
            LblApiStatus.TextColor = Color.FromArgb("#16A34A");
        }

        LblApiStatus.Text = statusText;
    }

    private static List<PoiDto> CreateFallbackPois() =>
    [
        // 1. Quán ốc — thương hiệu nổi tiếng nhất phố
        new PoiDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TenPOI = "Quán Ốc Oanh",
            KinhDo = 106.701831,
            ViDo   = 10.758955,
            BanKinh = 35,
            MucUuTien = 1,
            DiaChi = "234 Vĩnh Khánh, Q4, TP.HCM",
            SDT    = "0909 000 001",
            AnhDaiDien = null
        },
        // 2. Bò tơ — đặc sản bò nướng lá lốt
        new PoiDto
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TenPOI = "Bò Tơ Cô Út",
            KinhDo = 106.700942,
            ViDo   = 10.759512,
            BanKinh = 30,
            MucUuTien = 2,
            DiaChi = "215 Vĩnh Khánh, Q4, TP.HCM",
            SDT    = "0909 000 002",
            AnhDaiDien = null
        },
        // 3. Lẩu cá đuối — nét đặc trưng phố hải sản
        new PoiDto
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            TenPOI = "Lẩu Cá Đuối 404",
            KinhDo = 106.702114,
            ViDo   = 10.758312,
            BanKinh = 30,
            MucUuTien = 3,
            DiaChi = "404 Vĩnh Khánh, Q4, TP.HCM",
            SDT    = "0909 000 003",
            AnhDaiDien = null
        },
        // 4. Chè — đồ ngọt giải nhiệt
        new PoiDto
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            TenPOI = "Chè Khánh Vy",
            KinhDo = 106.701221,
            ViDo   = 10.759884,
            BanKinh = 25,
            MucUuTien = 4,
            DiaChi = "180 Vĩnh Khánh, Q4, TP.HCM",
            SDT    = "0909 000 004",
            AnhDaiDien = null
        },
        // 5. Hải sản nướng — mực, tôm, ghẹ
        new PoiDto
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            TenPOI = "Hải Sản Nướng Ba Phát",
            KinhDo = 106.702450,
            ViDo   = 10.759100,
            BanKinh = 30,
            MucUuTien = 5,
            DiaChi = "310 Vĩnh Khánh, Q4, TP.HCM",
            SDT    = "0909 000 005",
            AnhDaiDien = null
        },
        // 6. Bún bò Huế — món nước đặc sắc
        new PoiDto
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            TenPOI = "Bún Bò Huế Dì Sáu",
            KinhDo = 106.700560,
            ViDo   = 10.758640,
            BanKinh = 25,
            MucUuTien = 6,
            DiaChi = "152 Vĩnh Khánh, Q4, TP.HCM",
            SDT    = "0909 000 006",
            AnhDaiDien = null
        },
        // 7. Quán nhậu — mồi nhắm đặc sắc buổi tối
        new PoiDto
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            TenPOI = "Nhậu Vỉa Hè Năm Béo",
            KinhDo = 106.701605,
            ViDo   = 10.760210,
            BanKinh = 28,
            MucUuTien = 7,
            DiaChi = "275 Vĩnh Khánh, Q4, TP.HCM",
            SDT    = "0909 000 007",
            AnhDaiDien = null
        },
    ];

    private void StartGpsTracking()
    {
        _gpsCts?.Cancel();
        _gpsCts?.Dispose();
        _gpsCts = new CancellationTokenSource();
        var gpsCts = _gpsCts;
        var token = gpsCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var request = new GeolocationRequest(
                        GeolocationAccuracy.Best,
                        TimeSpan.FromSeconds(5));

                    var location = await Geolocation.Default.GetLocationAsync(request, token);
                    location ??= await Geolocation.Default.GetLastKnownLocationAsync();

                    if (location != null)
                    {
                        _userLocation = location;
                        CheckGeofence(location);

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            GpsStatusDot.BackgroundColor = Color.FromArgb("#4CAF50");
                            UpdateUserLocationOnMap(location);
                        });

                        // Heartbeat mỗi 15 giây (mỗi HEARTBEAT_EVERY_TICKS vòng)
                        _heartbeatTick++;
                        if (_heartbeatTick >= HEARTBEAT_EVERY_TICKS)
                        {
                            _heartbeatTick = 0;
                            _ = SendHeartbeatAsync(location.Latitude, location.Longitude);
                        }

                        // Tự động refresh danh sách POI mỗi 30 giây
                        _poiRefreshTick++;
                        if (_poiRefreshTick >= POI_REFRESH_EVERY_TICKS)
                        {
                            _poiRefreshTick = 0;
                            _ = RefreshPoisInBackgroundAsync();
                        }
                    }
                    else
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                            GpsStatusDot.BackgroundColor = Color.FromArgb("#AAAAAA"));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GPS error: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(() =>
                        GpsStatusDot.BackgroundColor = Color.FromArgb("#AAAAAA"));
                }

                try
                {
                    await Task.Delay(5000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    private async Task SendHeartbeatAsync(double lat, double lng)
    {
        try
        {
            var deviceId = Preferences.Get("device_id", "");
            if (string.IsNullOrEmpty(deviceId)) return;

            var body = new
            {
                MaThietBi    = deviceId,
                Lat          = lat,
                Lng          = lng,
                PoiIdHienTai  = _currentPoi?.Id,
                TenPoiHienTai = _currentPoi?.TenPOI
            };
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            await _http.PostAsJsonAsync($"{apiBaseUrl}/api/heartbeat", body);
        }
        catch
        {
            // Fire-and-forget: bỏ qua lỗi mạng
        }
    }

    /// <summary>
    /// Refresh POI ngầm mỗi 30 giây — phát hiện thêm/sửa/ẩn từ CMS mà không làm gián đoạn người dùng.
    /// </summary>
    private async Task RefreshPoisInBackgroundAsync()
    {
        try
        {
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var result = await _http.GetFromJsonAsync<List<PoiDto>>($"{apiBaseUrl}/api/poi");
            if (result == null) return;

            _pois = result;
            _isUsingFallbackData = false;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadMap();
                RenderPoiCards();
            });
        }
        catch
        {
            // Silent — đừng hiện dialog khi refresh ngầm thất bại
        }
    }

    /// <summary>Ghi nhận khách mở trang chi tiết POI lên server để CMS theo dõi.</summary>
    private async Task RecordPoiViewAsync(PoiDto poi)
    {
        try
        {
            var deviceId = Preferences.Get("device_id", "");
            if (string.IsNullOrEmpty(deviceId)) return;

            string lang = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var body = new { MaThietBi = deviceId, PoiId = poi.Id, NgonNgu = lang };
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var response = await _http.PostAsJsonAsync($"{apiBaseUrl}/api/heartbeat/view", body);
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // Fire-and-forget
        }
    }

    private async Task RecordPoiVisitAsync(PoiDto poi)
    {
        try
        {
            var deviceId = Preferences.Get("device_id", "");
            if (string.IsNullOrEmpty(deviceId)) return;

            string lang = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var body = new { MaThietBi = deviceId, PoiId = poi.Id, NgonNgu = lang };
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var response = await _http.PostAsJsonAsync($"{apiBaseUrl}/api/heartbeat/visit", body);
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // Fire-and-forget
        }
    }

    private async Task SyncPoiHistoryAsync()
    {
        if (!await _historySyncLock.WaitAsync(0))
            return;

        try
        {
            var deviceId = Preferences.Get("device_id", "");
            if (string.IsNullOrWhiteSpace(deviceId))
                return;

            static List<Guid> ParsePoiIds(HashSet<string> rawIds)
            {
                var result = new List<Guid>();
                foreach (var rawId in rawIds)
                {
                    if (Guid.TryParse(rawId, out var poiId) && poiId != Guid.Empty)
                        result.Add(poiId);
                }

                return result;
            }

            var viewedPoiIds = ParsePoiIds(GetSavedPoiIds(ViewedPoiIdsPreferenceKey));
            var visitedPoiIds = ParsePoiIds(GetSavedPoiIds(VisitedPoiIdsPreferenceKey));

            if (viewedPoiIds.Count == 0 && visitedPoiIds.Count == 0)
                return;

            string lang = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var body = new
            {
                MaThietBi = deviceId,
                ViewedPoiIds = viewedPoiIds,
                VisitedPoiIds = visitedPoiIds,
                NgonNgu = lang
            };

            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var response = await _http.PostAsJsonAsync($"{apiBaseUrl}/api/heartbeat/sync-history", body);
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // Silent sync retry on next refresh/open
        }
        finally
        {
            _historySyncLock.Release();
        }
    }

    private void CheckGeofence(Location userLoc)
    {
        PoiDto? nearest = null;
        double minDistance = double.MaxValue;

        foreach (var poi in _pois)
        {
            var poiLoc = new Location(poi.ViDo, poi.KinhDo);
            double distance = Location.CalculateDistance(userLoc, poiLoc, DistanceUnits.Kilometers) * 1000;

            if (distance < poi.BanKinh && distance < minDistance)
            {
                minDistance = distance;
                nearest = poi;
            }
        }

        if (nearest != null)
        {
            string poiKey = nearest.Id.ToString();
            bool isNewPoi = _currentPoi?.Id != nearest.Id;
            _currentPoi = nearest;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                HighlightNearestPoi(nearest.Id);
                RenderPoiCards();

                if (_sheetPoi?.Id == nearest.Id)
                    SheetNearBadge.IsVisible = true;
            });

            bool isCooledDown = !_lastSpokenTime.TryGetValue(poiKey, out DateTime lastTime)
                || (DateTime.UtcNow - lastTime).TotalMinutes >= COOLDOWN_MINUTES;

            if (isCooledDown && isNewPoi)
            {
                _lastSpokenTime[poiKey] = DateTime.UtcNow;
                RecordVisitedPoi(nearest);
                _ = SyncPoiHistoryAsync();

                Console.WriteLine($"[Geofence] Speak: {nearest.TenPOI}");
                // Ghi nhận lượt ghé thăm POI lên server (để admin theo dõi hành trình)
                _ = RecordPoiVisitAsync(nearest);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _ = SpeakPoiAsync(nearest);
                    HighlightNearestPoi(nearest.Id);
                    RenderPoiCards();
                    UpdateCaiDatUI();
                });
            }
            else if (!isCooledDown)
            {
                var remaining = COOLDOWN_MINUTES - (DateTime.UtcNow - _lastSpokenTime[poiKey]).TotalMinutes;
                Console.WriteLine($"[Cooldown] {nearest.TenPOI}: {remaining:F0} minutes left");
            }
        }
        else if (_currentPoi != null)
        {
            _currentPoi = null;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ClearHighlight();
                RenderPoiCards();
            });
        }
    }

    private async Task SpeakPoiAsync(PoiDto poi)
    {
        if (!await _speakLock.WaitAsync(0))
            return;

        try
        {
            string langCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            var response = await _http.GetAsync($"{apiBaseUrl}/api/thuyet-minh/{poi.Id}?lang={langCode}");

            string content = "";
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<ThuyetMinhResponse>();
                content = json?.NoiDung ?? "";
            }

            if (string.IsNullOrEmpty(content))
                content = GetText($"Chào mừng bạn đến {poi.TenPOI}", $"Welcome to {poi.TenPOI}", $"欢迎来到 {poi.TenPOI}");

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith(langCode))
                ?? locales.FirstOrDefault();

            _playingPoi = poi;
            ShowNowPlaying(poi);
            RenderPoiCards();

            await TextToSpeech.Default.SpeakAsync(content, new SpeechOptions { Locale = locale });
            _ = LogPlaybackAsync(poi.Id, langCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TTS] Error: {ex.Message}");
            HideNowPlaying();
            await TextToSpeech.Default.SpeakAsync($"Welcome to {poi.TenPOI}");
        }
        finally
        {
            _playingPoi = null;
            HideNowPlaying();
            RenderPoiCards();
            _speakLock.Release();
        }
    }

    private void ShowNowPlaying(PoiDto poi)
    {
        NowPlayingTitle.Text = GetText("Đang phát thuyết minh", "Now playing audio guide", "正在播放语音导览");
        NowPlayingName.Text = poi.TenPOI;
        NowPlayingBanner.IsVisible = true;
        _ = AnimateNowPlayingIcon();
    }

    private void HideNowPlaying()
    {
        NowPlayingBanner.IsVisible = false;
    }

    private async Task AnimateNowPlayingIcon()
    {
        string[] icons = ["AUDIO", "PLAY", "LIVE", "PLAY"];
        int i = 0;
        while (NowPlayingBanner.IsVisible)
        {
            NowPlayingIcon.Text = icons[i % icons.Length];
            i++;
            await Task.Delay(400);
        }

        NowPlayingIcon.Text = "AUDIO";
    }

    private async Task LogPlaybackAsync(Guid poiId, string langCode)
    {
        try
        {
            var apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            await _http.PostAsJsonAsync($"{apiBaseUrl}/api/log", new
            {
                POIId = poiId,
                NgonNguDung = langCode,
                ThoiGian = DateTime.UtcNow,
                Nguon = "GPS"
            });
            Console.WriteLine($"[Log] Playback saved: {poiId} ({langCode})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Log] Failed: {ex.Message}");
        }
    }

    private void LoadMap()
    {
        string latStr = "10.758955";
        string lngStr = "106.701831";

        var markersJs = new System.Text.StringBuilder();
        foreach (var poi in _pois)
        {
            string lat = poi.ViDo.ToString(CultureInfo.InvariantCulture);
            string lng = poi.KinhDo.ToString(CultureInfo.InvariantCulture);
            string poiId = poi.Id.ToString();
            string cleanId = poiId.Replace("-", "");

            markersJs.Append($@"
                L.circle([{lat},{lng}], {{
                    pane:'poiCircles',
                    radius: {poi.BanKinh}, color:'#FF6600',
                    fillColor:'#FF6600', fillOpacity:0.15, weight:2
                }}).addTo(map);
                var icon_{cleanId} = L.divIcon({{
                    html:'<div id=""poi_{cleanId}"" class=""poi-pin"" style=""cursor:pointer;transition:all .2s"">POI</div>',
                    iconSize:[36,36], className:''
                }});
                L.marker([{lat},{lng}],{{pane:'poiPins',zIndexOffset:200,icon:icon_{cleanId}}}).addTo(map)
                    .on('click',function(){{ window.location.href='poi://{poiId}'; }});
            ");
        }

        string html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        body{{margin:0;padding:0;overflow:hidden;}}
        #map{{width:100vw;height:100vh;}}
        .poi-pin{{width:36px;height:36px;border-radius:50%;display:flex;align-items:center;justify-content:center;background:#ff7a1a;color:white;font-size:12px;font-weight:800;box-shadow:0 6px 14px rgba(0,0,0,.24);border:3px solid rgba(255,255,255,.95);}}
        .user-dot{{width:20px;height:20px;background:#1565C0;border:3px solid white;border-radius:50%;box-shadow:0 0 0 8px rgba(21,101,192,0.22);}}
    </style>
</head>
<body>
<div id='map'></div>
<script>
    var map = L.map('map').setView([{latStr},{lngStr}], 16);
    L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',
        {{attribution:'OpenStreetMap',maxZoom:19}}).addTo(map);

    map.createPane('poiCircles');
    map.getPane('poiCircles').style.zIndex = 350;
    map.createPane('poiPins');
    map.getPane('poiPins').style.zIndex = 420;
    map.createPane('userLayer');
    map.getPane('userLayer').style.zIndex = 650;

    {markersJs}

    var userMarker=null, accCircle=null;
    function setUserPos(lat,lng,acc){{
        if(userMarker) map.removeLayer(userMarker);
        if(accCircle) map.removeLayer(accCircle);
        userMarker=L.marker([lat,lng],{{
            pane:'userLayer',
            zIndexOffset:1000,
            icon:L.divIcon({{html:'<div class=""user-dot""></div>',iconSize:[20,20],iconAnchor:[10,10],className:''}})
        }}).addTo(map);
        if(acc&&acc<300) accCircle=L.circle([lat,lng],{{pane:'userLayer',radius:acc,color:'#1565C0',fillColor:'#1565C0',fillOpacity:0.08,weight:1}}).addTo(map);
        userMarker && userMarker.setZIndexOffset(1000);
        window.location.href='location://'+lat+','+lng;
    }}
    function updateUserLocation(lat,lng){{ setUserPos(lat,lng,20); }}
    function highlightPoi(id){{
        var el=document.getElementById('poi_'+id.replace(/-/g,''));
        if(el){{el.style.background='#FF6600';el.style.transform='scale(1.25)';}}
    }}
    function clearHighlight(){{
        document.querySelectorAll('[id^=""poi_""]').forEach(function(el){{
            el.style.background='#2196F3';el.style.transform='scale(1)';
        }});
    }}
</script>
</body>
</html>";

        MapWebView.Source = new HtmlWebViewSource { Html = html };
        MapWebView.Navigating -= OnMapNavigating;
        MapWebView.Navigating += OnMapNavigating;
        MapWebView.Navigated -= OnMapNavigated;
        MapWebView.Navigated += OnMapNavigated;
    }

    private void OnMapNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _isMapReady = true;

        if (_pendingMapLocation != null)
        {
            UpdateUserLocationOnMap(_pendingMapLocation);
            _pendingMapLocation = null;
        }

        if (_pendingHighlightPoiId.HasValue)
        {
            HighlightNearestPoi(_pendingHighlightPoiId.Value);
            _pendingHighlightPoiId = null;
        }
    }

    private void HighlightNearestPoi(Guid poiId)
    {
        if (!_isMapReady)
        {
            _pendingHighlightPoiId = poiId;
            return;
        }

        MapWebView.Eval($"highlightPoi('{poiId}'); true;");
    }

    private void ClearHighlight()
    {
        if (!_isMapReady)
        {
            _pendingHighlightPoiId = null;
            return;
        }

        MapWebView.Eval("clearHighlight(); true;");
    }

    private void UpdateUserLocationOnMap(Location loc)
    {
        if (!_isMapReady)
        {
            _pendingMapLocation = loc;
            return;
        }

        string lat = loc.Latitude.ToString(CultureInfo.InvariantCulture);
        string lng = loc.Longitude.ToString(CultureInfo.InvariantCulture);
        MapWebView.Eval($"updateUserLocation({lat},{lng}); true;");
    }

    private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("location://"))
        {
            e.Cancel = true;
            var coords = e.Url.Replace("location://", "").Split(',');
            if (coords.Length == 2 &&
                double.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lng))
            {
                var loc = new Location(lat, lng);
                _userLocation = loc;
                GpsStatusDot.BackgroundColor = Color.FromArgb("#4CAF50");
                CheckGeofence(loc);
            }
            return;
        }

        if (e.Url.StartsWith("poi://"))
        {
            e.Cancel = true;
            string poiIdStr = e.Url.Replace("poi://", "").Trim('/');
            if (Guid.TryParse(poiIdStr, out Guid poiId))
            {
                var poi = _pois.FirstOrDefault(p => p.Id == poiId);
                if (poi != null)
                    ShowMapPoiSheet(poi);
            }
            return;
        }

        if (e.Url.StartsWith("googlemaps://"))
        {
            e.Cancel = true;
            try
            {
                var query = e.Url.Replace("googlemaps://?q=", "");
                await Browser.Default.OpenAsync($"https://maps.google.com/?q={query}", BrowserLaunchMode.External);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Open Maps error: {ex.Message}");
            }
        }
    }

    private void ShowMapPoiSheet(PoiDto poi)
    {
        _sheetPoi = poi;

        SheetTen.Text = poi.TenPOI;
        SheetDiaChi.Text = $"{GetText("Địa chỉ", "Address", "地址")}: {poi.DiaChi ?? GetText("Phố Vĩnh Khánh, Quận 4", "Vinh Khanh Street, District 4", "荣庆街，第4区")}";
        SheetSDT.Text = string.IsNullOrEmpty(poi.SDT) ? "" : $"Hotline: {poi.SDT}";

        SheetImg.Source = FoodImageCatalog.GetPoiImageSource(poi.AnhDaiDien, poi.TenPOI);

        SheetNearBadge.IsVisible = _currentPoi?.Id == poi.Id || _playingPoi?.Id == poi.Id;

        MapPoiSheet.TranslationY = 300;
        MapPoiSheet.IsVisible = true;
        _ = MapPoiSheet.TranslateToAsync(0, 0, 280, Easing.CubicOut);
    }

    private async void OnSheetCloseClicked(object? sender, EventArgs e)
    {
        await MapPoiSheet.TranslateToAsync(0, 300, 200, Easing.CubicIn);
        MapPoiSheet.IsVisible = false;
        _sheetPoi = null;
    }

    private async void OnSheetDetailClicked(object? sender, EventArgs e)
    {
        if (_sheetPoi == null)
            return;

        await MapPoiSheet.TranslateToAsync(0, 300, 180, Easing.CubicIn);
        MapPoiSheet.IsVisible = false;
        await OpenPoiDetailAsync(_sheetPoi);
    }

    private async void OnSheetMapClicked(object? sender, EventArgs e)
    {
        if (_sheetPoi == null)
            return;

        string lat = _sheetPoi.ViDo.ToString(CultureInfo.InvariantCulture);
        string lng = _sheetPoi.KinhDo.ToString(CultureInfo.InvariantCulture);
        await Browser.Default.OpenAsync($"https://maps.google.com/?q={lat},{lng}", BrowserLaunchMode.External);
    }

    private async void OnNowPlayingDetailClicked(object? sender, EventArgs e)
    {
        var poi = _playingPoi ?? _currentPoi;
        if (poi == null)
            return;

        await OpenPoiDetailAsync(poi);
    }

    private IReadOnlyList<PoiDto> GetVisiblePois()
    {
        IEnumerable<PoiDto> source = _pois;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            string keyword = NormalizeSearchText(_searchText);
            source = source.Where(p =>
                MatchesSearch(p.TenPOI, keyword) ||
                MatchesSearch(p.DiaChi, keyword) ||
                MatchesSearch(p.SDT, keyword));
        }

        // Quán đang trong vùng geofence (đang tô cam) luôn nổi lên đầu danh sách
        if (_currentPoi != null)
            source = source.OrderByDescending(p => p.Id == _currentPoi.Id);

        return source.ToList();
    }

    private (string Label, Color Accent) GetPoiVisualMeta(PoiDto poi)
    {
        var text = NormalizeSearchText(poi.TenPOI);

        if (text.Contains("oc") || text.Contains("ngheu") || text.Contains("hai san"))
            return (GetText("Hai san", "Seafood", "Hai xian"), Color.FromArgb("#0F766E"));

        if (text.Contains("bo") || text.Contains("nuong"))
            return (GetText("Nuong", "Grill", "Shao kao"), Color.FromArgb("#B45309"));

        if (text.Contains("lau"))
            return (GetText("Lau", "Hotpot", "Huo guo"), Color.FromArgb("#7C3AED"));

        if (text.Contains("che") || text.Contains("tra sua"))
            return (GetText("Trang mieng", "Dessert", "Tian pin"), Color.FromArgb("#DB2777"));

        if (text.Contains("bun") || text.Contains("pho") || text.Contains("mi") || text.Contains("hu tieu"))
            return (GetText("Mon nuoc", "Noodles", "Mian shi"), Color.FromArgb("#2563EB"));

        if (text.Contains("nhau") || text.Contains("via he") || text.Contains("an vat"))
            return (GetText("Pho dem", "Street food", "Ye shi"), Color.FromArgb("#EA580C"));

        return (GetText("Quan an", "Eatery", "Can ting"), Color.FromArgb("#475569"));
    }

    private void RenderPoiCards()
    {
        PoiListContainer.Children.Clear();
        var visiblePois = GetVisiblePois();

        if (visiblePois.Count == 0)
        {
            LblPoiCount.Text = _pois.Count > 0
                ? GetText("Không tìm thấy kết quả", "No results", "没有结果")
                : "";

            PoiListContainer.Children.Add(new Label
            {
                Text = GetText(
                    "Không có địa điểm phù hợp với từ khóa tìm kiếm.",
                    "No places match your search.",
                    "没有符合搜索条件的地点。"),
                FontSize = 14,
                TextColor = Color.FromArgb("#666666"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 28, 0, 0)
            });
            return;
        }

        LblPoiCount.Text = string.IsNullOrWhiteSpace(_searchText)
            ? GetText($"{visiblePois.Count} địa điểm", $"{visiblePois.Count} places", $"{visiblePois.Count} 个地点")
            : GetText($"{visiblePois.Count}/{_pois.Count} kết quả", $"{visiblePois.Count}/{_pois.Count} results", $"{visiblePois.Count}/{_pois.Count} 条结果");

        foreach (var poi in visiblePois)
        {
            string distanceText = GetText("Đang xác định...", "Calculating...", "正在计算...");
            if (_userLocation != null)
            {
                double distM = Location.CalculateDistance(
                    _userLocation,
                    new Location(poi.ViDo, poi.KinhDo),
                    DistanceUnits.Kilometers) * 1000;

                distanceText = distM < 1000
                    ? GetText($"Cách bạn: {(int)distM}m", $"{(int)distM}m away", $"距离 {(int)distM} 米")
                    : GetText($"Cách bạn: {distM / 1000:F1}km", $"{distM / 1000:F1}km away", $"距离 {distM / 1000:F1} 公里");
            }

            bool isNear = _currentPoi?.Id == poi.Id;
            bool isPlaying = _playingPoi?.Id == poi.Id;
            var visualMeta = GetPoiVisualMeta(poi);

            var card = new Border
            {
                Stroke = isNear ? visualMeta.Accent : Color.FromArgb("#ECE7E1"),
                StrokeThickness = isNear ? 2 : 1,
                BackgroundColor = Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Shadow = new Shadow
                {
                    Brush = Colors.Black,
                    Offset = new Point(0, 4),
                    Opacity = isNear ? 0.14f : 0.07f,
                    Radius = 10
                },
                Padding = 0
            };

            var grid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = 172 },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };

            var media = new Grid();
            Grid.SetRow(media, 0);

            var imageFrame = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#FFF7ED"),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20, 20, 0, 0) }
            };

            var img = new Image
            {
                Source = FoodImageCatalog.GetPoiImageSource(poi.AnhDaiDien, poi.TenPOI),
                Aspect = Aspect.AspectFill
            };
            imageFrame.Content = img;
            media.Children.Add(imageFrame);

            var imageShade = new BoxView
            {
                VerticalOptions = LayoutOptions.End,
                HeightRequest = 88,
                Opacity = 0.95
            };
            imageShade.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Colors.Transparent, 0.0f),
                    new GradientStop(Color.FromArgb("#C2191A1A"), 1.0f)
                },
                new Point(0, 0),
                new Point(0, 1));
            media.Children.Add(imageShade);

            var imageBadge = new Border
            {
                BackgroundColor = Color.FromRgba(
                    visualMeta.Accent.Red,
                    visualMeta.Accent.Green,
                    visualMeta.Accent.Blue,
                    0.92f),
                Stroke = Colors.Transparent,
                Padding = new Thickness(10, 5),
                Margin = new Thickness(14, 14, 0, 0),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                StrokeShape = new RoundRectangle { CornerRadius = 999 }
            };
            imageBadge.Content = new Label
            {
                Text = visualMeta.Label,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            };
            media.Children.Add(imageBadge);

            var distanceChip = new Border
            {
                BackgroundColor = Color.FromArgb("#CC111827"),
                Stroke = Colors.Transparent,
                Padding = new Thickness(10, 5),
                Margin = new Thickness(14, 0, 14, 14),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.End,
                StrokeShape = new RoundRectangle { CornerRadius = 999 }
            };
            distanceChip.Content = new Label
            {
                Text = distanceText,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            };
            media.Children.Add(distanceChip);

            var info = new VerticalStackLayout
            {
                Padding = new Thickness(16, 14, 16, 16),
                Spacing = 9
            };

            if (isNear || isPlaying)
            {
                info.Children.Add(new Label
                {
                    Text = GetText("Đang gần - tự động phát audio", "Nearby - auto audio", "附近 - 自动语音"),
                    FontSize = 11,
                    TextColor = visualMeta.Accent,
                    FontAttributes = FontAttributes.Bold
                });
            }

            info.Children.Add(new Label
            {
                Text = poi.TenPOI,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black,
                MaxLines = 2,
                LineBreakMode = LineBreakMode.WordWrap
            });

            info.Children.Add(new Label
            {
                Text = $"{distanceText} - {poi.DiaChi ?? GetText("Phố Vĩnh Khánh", "Vinh Khanh Street", "荣庆街")}",
                FontSize = 13,
                TextColor = Color.FromArgb("#666666"),
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation
            });

            var btn = new Button
            {
                Text = GetText("Xem chi tiết", "View details", "查看详情"),
                BackgroundColor = isNear ? visualMeta.Accent : Color.FromArgb("#1F2937"),
                TextColor = Colors.White,
                CornerRadius = 10,
                Margin = new Thickness(0, 8, 0, 0),
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 42
            };

            var capturedPoi = poi;
            btn.Clicked += async (s, e) => await OpenPoiDetailAsync(capturedPoi);

            info.Children.Add(btn);
            Grid.SetRow(info, 1);

            grid.Children.Add(media);
            grid.Children.Add(info);
            card.Content = grid;
            PoiListContainer.Children.Add(card);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue ?? "";
        RenderPoiCards();
    }

    private void OnTabKhamPhaTapped(object? sender, TappedEventArgs e)
    {
        ViewKhamPha.IsVisible = true;
        ViewBanDo.IsVisible = false;
        ViewCaiDat.IsVisible = false;
        SearchBarRow.IsVisible = true;
        SetTabActive("khampha");
    }

    private void OnTabBanDoTapped(object? sender, TappedEventArgs e)
    {
        ViewKhamPha.IsVisible = false;
        ViewBanDo.IsVisible = true;
        ViewCaiDat.IsVisible = false;
        SearchBarRow.IsVisible = false;
        SetTabActive("bando");
    }

    private void OnTabCaiDatTapped(object? sender, TappedEventArgs e)
    {
        ViewKhamPha.IsVisible = false;
        ViewBanDo.IsVisible = false;
        ViewCaiDat.IsVisible = true;
        SearchBarRow.IsVisible = false;
        UpdateCaiDatUI();
        SetTabActive("caidat");
    }

    private void UpdateCaiDatUI()
    {
        ApplyLocalizedUiText();

        string lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        // Avatar initials — 2 ký tự đầu của device ID
        var deviceId = Preferences.Get("device_id", "");
        var initials = string.IsNullOrEmpty(deviceId) ? "VK"
            : deviceId[..Math.Min(2, deviceId.Length)].ToUpper();
        LblUserName.Text = initials;

        // Chế độ / role (hiển thị trong settings row)
        LblUserUsername.Text = GetText("Khách ẩn danh", "Anonymous guest", "匿名游客");
        LblUserRole.Text     = GetText("Thám tử ẩm thực · Cấp 1", "Food Explorer · Lv 1", "美食探索者·1级");

        LblUserLang.Text = lang switch
        {
            "en" => "English",
            "zh" => "中文",
            _ => "Tiếng Việt"
        };

        // Gói đăng ký: hiển thị số ngày còn lại
        var hetHanStr = Preferences.Get("sub_ngay_het_han", "");
        if (!string.IsNullOrEmpty(hetHanStr) &&
            DateTime.TryParse(hetHanStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var hetHan) &&
            hetHan > DateTime.UtcNow)
        {
            int soNgay = Math.Max(1, (int)(hetHan - DateTime.UtcNow).TotalDays + 1);
            LblSubStatus.Text = GetText($"Còn {soNgay} ngày", $"{soNgay} days left", $"剩余 {soNgay} 天");
            LblSubStatus.TextColor = Color.FromArgb("#16A34A");
        }
        else
        {
            LblSubStatus.Text = GetText("Chưa kích hoạt", "Not activated", "未激活");
            LblSubStatus.TextColor = Color.FromArgb("#DC2626");
        }

        // Mã thiết bị (8 ký tự đầu của UUID)
        LblUserPhone.Text = string.IsNullOrEmpty(deviceId)
            ? "—"
            : deviceId[..Math.Min(8, deviceId.Length)].ToUpper();

        // Số quán đã ghé (đọc từ LichSuPhat qua bộ nhớ local — cập nhật khi reload)
        var viewedPoiCount = GetSavedPoiCount(ViewedPoiIdsPreferenceKey);
        var visitedPoiCount = GetSavedPoiCount(VisitedPoiIdsPreferenceKey);
        var totalXp = (viewedPoiCount * 50) + (visitedPoiCount * 100);
        var currentLevel = Math.Max(1, (totalXp / 500) + 1);
        var xpInCurrentLevel = totalXp % 500;
        var xpProgress = Math.Clamp(xpInCurrentLevel / 500d, 0d, 1d);

        if (LblStatQuanXemV2 is not null)
            LblStatQuanXemV2.Text = viewedPoiCount.ToString();

        if (LblStatQuanGheV2 is not null)
            LblStatQuanGheV2.Text = visitedPoiCount.ToString();

        if (LblStatQuanXem is not null)
            LblStatQuanXem.Text = viewedPoiCount.ToString();

        if (LblXpProgress is not null)
            LblXpProgress.Text = $"XP: {xpInCurrentLevel} / 500";

        if (ProfileXpBar is not null)
            ProfileXpBar.Progress = xpProgress;

        LblUserRole.Text = GetText(
            $"Thám tử ẩm thực · Cấp {currentLevel}",
            $"Food Explorer · Lv {currentLevel}",
            $"美食探索者·{currentLevel}级");

        UpdateApiConnectionUi();
    }

    private async void OnGiaHanClicked(object? sender, EventArgs? e)
    {
        await Navigation.PushModalAsync(new SubscriptionPage(hetHan: false), animated: true);
    }

    private async void OnReloadDataClicked(object? sender, EventArgs e)
    {
        _gpsCts?.Cancel();
        _gpsCts?.Dispose();
        _gpsCts = null;

        _currentPoi = null;
        _playingPoi = null;
        _userLocation = null;
        _isMapReady = false;
        _pendingMapLocation = null;
        _pendingHighlightPoiId = null;

        await LoadPoisFromApi(showFailureAlert: true);
        await EnsureGpsTrackingAsync();
        UpdateCaiDatUI();

        if (!_isUsingFallbackData)
        {
            await DisplayAlertAsync(
            GetText("Thông báo", "Notice", "提示"),
            GetText("Đã tải lại dữ liệu mới nhất.", "Latest data reloaded.", "已重新加载最新数据。"),
            GetText("OK", "OK", "确定"));
        }
    }

    private async void OnSaveApiUrlClicked(object? sender, EventArgs e)
    {
        var normalized = AppConfig.NormalizeApiBaseUrl(EntryApiBaseUrl.Text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            await DisplayAlertAsync(
                GetText("URL khong hop le", "Invalid URL", "URL wu xiao"),
                GetText("Hay nhap day du giao thuc va cong, vi du http://192.168.1.5:5118.", "Enter the full URL including protocol and port, for example http://192.168.1.5:5118.", "Qing shu ru wan zheng URL, bao gom xie yi va duan kou, li ru http://192.168.1.5:5118."),
                "OK");
            return;
        }

        AppConfig.SetCustomApiBaseUrl(normalized);
        await LoadPoisFromApi(showFailureAlert: true);
        UpdateCaiDatUI();
    }

    private async void OnResetApiUrlClicked(object? sender, EventArgs e)
    {
        AppConfig.ClearCustomApiBaseUrl();
        EntryApiBaseUrl.Text = "";
        await LoadPoisFromApi(showFailureAlert: true);
        UpdateCaiDatUI();
    }

    private void SetTabActive(string tab)
    {
        BtnKhamPha.Opacity = 0.45;
        BtnBanDo.Opacity = 0.45;
        BtnCaiDat.Opacity = 0.45;
        LblKhamPha.TextColor = Color.FromArgb("#888");
        LblBanDo.TextColor = Color.FromArgb("#888");
        LblCaiDat.TextColor = Color.FromArgb("#888");

        switch (tab)
        {
            case "khampha":
                BtnKhamPha.Opacity = 1.0;
                LblKhamPha.TextColor = Color.FromArgb("#FF5722");
                break;
            case "bando":
                BtnBanDo.Opacity = 1.0;
                LblBanDo.TextColor = Color.FromArgb("#FF5722");
                break;
            case "caidat":
                BtnCaiDat.Opacity = 1.0;
                LblCaiDat.TextColor = Color.FromArgb("#FF5722");
                break;
        }
    }

    private async void OnListenClicked(object? sender, EventArgs e)
    {
        if (_currentPoi != null)
        {
            await SpeakPoiAsync(_currentPoi);
            return;
        }

        await DisplayAlertAsync(
            GetText("Thông báo", "Notice", "提示"),
            GetText(
                "Hãy đến gần một quán ăn để nghe thuyết minh.",
                "Move closer to a place to start the audio guide.",
                "请靠近一个地点以开始语音导览。"),
            GetText("OK", "OK", "确定"));
    }
}

public class ThuyetMinhResponse
{
    public string NoiDung { get; set; } = "";
    public string? FileAudio { get; set; }
    public string NgonNgu { get; set; } = "vi";
}
