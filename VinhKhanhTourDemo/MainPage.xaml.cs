using Microsoft.Maui.Controls.Shapes;
using System.Globalization;
using System.Net.Http.Json;
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
    private static string API_BASE => AppConfig.ApiBaseUrl;
    private const int COOLDOWN_MINUTES = 10;

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
    private readonly Dictionary<string, DateTime> _lastSpokenTime = new();

    public MainPage()
    {
        InitializeComponent();
        ApplyLocalizedUiText();
        UpdateCaiDatUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            _isInitialized = true;
            await LoadPoisFromApi();
        }

        await EnsureGpsTrackingAsync();
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
        LblHeaderSub.Text = GetText("Kham pha pho am thuc", "Discover the food street", "探索美食街");
        NowPlayingTitle.Text = GetText("Dang phat thuyet minh", "Now playing audio guide", "正在播放语音导览");
        BtnNowPlayingDetail.Text = GetText("Chi tiet", "Details", "详情");
        SearchEntry.Placeholder = GetText("Tim quan an, dia chi...", "Search places, address...", "搜索店名、地址...");
        LblNearbyTitle.Text = GetText("Diem den gan ban", "Nearby places", "附近地点");
        LblSheetNearBadge.Text = GetText("Dang trong vung audio", "Inside audio zone", "已进入语音范围");
        SheetBtnDetail.Text = GetText("Xem chi tiet", "View details", "查看详情");
        SheetBtnMap.Text = GetText("Chi duong", "Directions", "导航");
        BtnSheetClose.Text = GetText("Dong", "Close", "关闭");
        LblKhamPha.Text = GetText("Kham pha", "Explore", "探索");
        LblBanDo.Text = GetText("Ban do", "Map", "地图");
        LblCaiDat.Text = GetText("Cai dat", "Settings", "设置");
        BtnReloadData.Text = GetText("Tai lai du lieu", "Reload data", "重新加载数据");
        LblModeCaption.Text = GetText("Che do", "Mode", "模式");
        LblLocalDataCaption.Text = GetText("Du lieu cuc bo", "Local data", "本地数据");
        LblApiCaption.Text = "API server";
        LblLanguageCaption.Text = GetText("Ngon ngu thuyet minh", "Audio language", "语音语言");
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

    private async Task LoadPoisFromApi()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<PoiDto>>($"{API_BASE}/api/poi");
            if (result != null)
            {
                _pois = result;
                Console.WriteLine($"=== Loaded {_pois.Count} POI ===");
                foreach (var p in _pois)
                    Console.WriteLine($"  - {p.TenPOI}: {p.ViDo}, {p.KinhDo}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API error, fallback to local sample: {ex.Message}");
            _pois = CreateFallbackPois();

            await DisplayAlertAsync(
                GetText("Khong tai duoc du lieu", "Unable to load data", "无法加载数据"),
                GetText(
                    "API hoac database dang loi. Ung dung tam dung du lieu mau de ban tiep tuc kiem tra giao dien.",
                    "The API or database is failing. The app is temporarily using sample data so you can continue testing the UI.",
                    "API 或数据库发生错误。应用暂时使用示例数据，方便你继续测试界面。"),
                "OK");
        }

        LoadMap();
        RenderPoiCards();
    }

    private static List<PoiDto> CreateFallbackPois() =>
    [
        new PoiDto
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TenPOI = "Quan Oc Oanh",
            KinhDo = 106.701831,
            ViDo = 10.758955,
            BanKinh = 30,
            MucUuTien = 1,
            DiaChi = "234 Vinh Khanh, Q4",
            SDT = "0909 000 001"
        },
        new PoiDto
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TenPOI = "Bo To Co Ut",
            KinhDo = 106.700942,
            ViDo = 10.759512,
            BanKinh = 30,
            MucUuTien = 2,
            DiaChi = "215 Vinh Khanh, Q4",
            SDT = "0909 000 002"
        },
        new PoiDto
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            TenPOI = "Lau Ca Duoi 404",
            KinhDo = 106.702114,
            ViDo = 10.758312,
            BanKinh = 30,
            MucUuTien = 3,
            DiaChi = "404 Vinh Khanh, Q4",
            SDT = "0909 000 003"
        },
        new PoiDto
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            TenPOI = "Che Khanh Vy",
            KinhDo = 106.701221,
            ViDo = 10.759884,
            BanKinh = 25,
            MucUuTien = 4,
            DiaChi = "180 Vinh Khanh, Q4",
            SDT = "0909 000 004"
        }
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

                Console.WriteLine($"[Geofence] Speak: {nearest.TenPOI}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _ = SpeakPoiAsync(nearest);
                    HighlightNearestPoi(nearest.Id);
                    RenderPoiCards();
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

            var response = await _http.GetAsync($"{API_BASE}/api/thuyet-minh/{poi.Id}?lang={langCode}");

            string content = "";
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<ThuyetMinhResponse>();
                content = json?.NoiDung ?? "";
            }

            if (string.IsNullOrEmpty(content))
                content = GetText($"Chao mung ban den {poi.TenPOI}", $"Welcome to {poi.TenPOI}", $"欢迎来到 {poi.TenPOI}");

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
        NowPlayingTitle.Text = GetText("Dang phat thuyet minh", "Now playing audio guide", "正在播放语音导览");
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
            await _http.PostAsJsonAsync($"{API_BASE}/api/log", new
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
        SheetDiaChi.Text = $"{GetText("Dia chi", "Address", "地址")}: {poi.DiaChi ?? GetText("Pho Vinh Khanh, Quan 4", "Vinh Khanh Street, District 4", "荣庆街，第4区")}";
        SheetSDT.Text = string.IsNullOrEmpty(poi.SDT) ? "" : $"Hotline: {poi.SDT}";

        SheetImg.Source = string.IsNullOrEmpty(poi.AnhDaiDien)
            ? ImageSource.FromUri(new Uri("https://images.unsplash.com/photo-1555396273-367ea4eb4db5?w=400"))
            : ImageSource.FromUri(new Uri(poi.AnhDaiDien));

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
        await Navigation.PushAsync(new DetailPage(_sheetPoi));
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

        await Navigation.PushAsync(new DetailPage(poi));
    }

    private IReadOnlyList<PoiDto> GetVisiblePois()
    {
        if (string.IsNullOrWhiteSpace(_searchText))
            return _pois;

        string keyword = _searchText.Trim();

        return _pois
            .Where(p =>
                (!string.IsNullOrWhiteSpace(p.TenPOI) &&
                 p.TenPOI.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(p.DiaChi) &&
                 p.DiaChi.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(p.SDT) &&
                 p.SDT.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
    }

    private void RenderPoiCards()
    {
        PoiListContainer.Children.Clear();
        var visiblePois = GetVisiblePois();

        if (visiblePois.Count == 0)
        {
            LblPoiCount.Text = _pois.Count > 0
                ? GetText("Khong tim thay ket qua", "No results", "没有结果")
                : "";

            PoiListContainer.Children.Add(new Label
            {
                Text = GetText(
                    "Khong co dia diem phu hop voi tu khoa tim kiem.",
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
            ? GetText($"{visiblePois.Count} dia diem", $"{visiblePois.Count} places", $"{visiblePois.Count} 个地点")
            : GetText($"{visiblePois.Count}/{_pois.Count} ket qua", $"{visiblePois.Count}/{_pois.Count} results", $"{visiblePois.Count}/{_pois.Count} 条结果");

        foreach (var poi in visiblePois)
        {
            string distanceText = GetText("Dang xac dinh...", "Calculating...", "正在计算...");
            if (_userLocation != null)
            {
                double distM = Location.CalculateDistance(
                    _userLocation,
                    new Location(poi.ViDo, poi.KinhDo),
                    DistanceUnits.Kilometers) * 1000;

                distanceText = distM < 1000
                    ? GetText($"Cach ban: {(int)distM}m", $"{(int)distM}m away", $"距离 {(int)distM} 米")
                    : GetText($"Cach ban: {distM / 1000:F1}km", $"{distM / 1000:F1}km away", $"距离 {distM / 1000:F1} 公里");
            }

            bool isNear = _currentPoi?.Id == poi.Id;
            bool isPlaying = _playingPoi?.Id == poi.Id;

            var card = new Border
            {
                Stroke = isNear ? Color.FromArgb("#FF6600") : Colors.Transparent,
                StrokeThickness = isNear ? 2 : 0,
                BackgroundColor = Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 15 },
                Shadow = new Shadow
                {
                    Brush = Colors.Black,
                    Offset = new Point(0, 2),
                    Opacity = isNear ? 0.15f : 0.08f,
                    Radius = 8
                }
            };

            var grid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = 160 },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };

            var img = new Image
            {
                Source = string.IsNullOrEmpty(poi.AnhDaiDien)
                    ? "https://images.unsplash.com/photo-1555396273-367ea4eb4db5?q=80&w=800"
                    : poi.AnhDaiDien,
                Aspect = Aspect.AspectFill
            };
            Grid.SetRow(img, 0);

            var info = new VerticalStackLayout
            {
                Padding = new Thickness(15),
                Spacing = 8
            };

            if (isNear || isPlaying)
            {
                info.Children.Add(new Label
                {
                    Text = GetText("Dang gan - tu dong phat audio", "Nearby - auto audio", "附近 - 自动语音"),
                    FontSize = 11,
                    TextColor = Color.FromArgb("#FF6600"),
                    FontAttributes = FontAttributes.Bold
                });
            }

            info.Children.Add(new Label
            {
                Text = poi.TenPOI,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            });

            info.Children.Add(new Label
            {
                Text = $"{distanceText} - {poi.DiaChi ?? GetText("Pho Vinh Khanh", "Vinh Khanh Street", "荣庆街")}",
                FontSize = 13,
                TextColor = Color.FromArgb("#666666")
            });

            var btn = new Button
            {
                Text = GetText("Xem chi tiet", "View details", "查看详情"),
                BackgroundColor = isNear ? Color.FromArgb("#FF6600") : Color.FromArgb("#2196F3"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Margin = new Thickness(0, 8, 0, 0),
                FontAttributes = FontAttributes.Bold
            };

            var capturedPoi = poi;
            btn.Clicked += async (s, e) => await Navigation.PushAsync(new DetailPage(capturedPoi));

            info.Children.Add(btn);
            Grid.SetRow(info, 1);

            grid.Children.Add(img);
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
        LblUserName.Text = GetText("Khach tham quan", "Visitor", "游客");
        LblUserUsername.Text = GetText("Che do khach", "Guest mode", "游客模式");
        LblUserPhone.Text = GetText("Khong luu", "Not stored", "未保存");
        LblUserEmail.Text = AppConfig.ApiBaseUrl;
        LblUserRole.Text = GetText("Che do tham quan", "Tour guest mode", "游客模式");

        LblUserLang.Text = lang switch
        {
            "en" => "English",
            "zh" => "中文",
            _ => "Tieng Viet"
        };
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

        await LoadPoisFromApi();
        await EnsureGpsTrackingAsync();
        UpdateCaiDatUI();

        await DisplayAlertAsync(
            GetText("Thong bao", "Notice", "提示"),
            GetText("Da tai lai du lieu moi nhat.", "Latest data reloaded.", "已重新加载最新数据。"),
            GetText("OK", "OK", "确定"));
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
            GetText("Thong bao", "Notice", "提示"),
            GetText(
                "Hay den gan mot quan an de nghe thuyet minh.",
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
