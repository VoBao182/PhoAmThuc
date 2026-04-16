using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanhTourDemo;

public class PoiDetailDto
{
    public Guid Id { get; set; }
    public string TenPOI { get; set; } = "";
    public string? DiaChi { get; set; }
    public string? SDT { get; set; }
    public string? AnhDaiDien { get; set; }
    public string NoiDungThuyetMinh { get; set; } = "";
    public string? FileAudio { get; set; }
    public double ViDo { get; set; }
    public double KinhDo { get; set; }
    public List<MonAnDto> MonAns { get; set; } = [];
}

public class MonAnDto
{
    public string TenMonAn { get; set; } = "";
    public decimal DonGia { get; set; }
    public string? PhanLoai { get; set; }
    public string? MoTa { get; set; }
    public string? HinhAnh { get; set; }
}

// ──────────────────────────────────────────────
//  STRINGS ĐA NGÔN NGỮ  (vi / en / zh)
// ──────────────────────────────────────────────
public static class AppStrings
{
    private static string _lang = "vi";

    public static void SetLang(string twoLetter)
    {
        _lang = twoLetter switch
        {
            "en" => "en",
            "zh" => "zh",
            _    => "vi"          // mặc định tiếng Việt
        };
    }

    public static string SectionIntro => _lang switch
    {
        "en" => "About",
        "zh" => "简介",
        _    => "Giới thiệu"
    };

    public static string SectionMenu => _lang switch
    {
        "en" => "Menu",
        "zh" => "菜单",
        _    => "Thực đơn"
    };

    public static string BtnListen => _lang switch
    {
        "en" => "🎧 Audio Guide",
        "zh" => "🎧 语音导览",
        _    => "🎧 Nghe thuyết minh"
    };

    public static string BtnListening => _lang switch
    {
        "en" => "🔊 Playing...",
        "zh" => "🔊 播放中...",
        _    => "🔊 Đang phát..."
    };

    public static string BtnDirection => _lang switch
    {
        "en" => "📍 Directions",
        "zh" => "📍 导航",
        _    => "📍 Chỉ đường"
    };

    public static string NoAudio => _lang switch
    {
        "en" => "No audio guide available.",
        "zh" => "暂无语音导览。",
        _    => "Chưa có nội dung thuyết minh."
    };

    public static string NoMenu => _lang switch
    {
        "en" => "Menu not available",
        "zh" => "暂无菜单",
        _    => "Chưa có thực đơn"
    };

    public static string DefaultAddress => _lang switch
    {
        "en" => "Vinh Khanh Street, District 4",
        "zh" => "荣庆街，第四区",
        _    => "Phố Vĩnh Khánh, Quận 4"
    };

    public static string FallbackIntro(string name) => _lang switch
    {
        "en" => $"Welcome to {name}. This is one of the famous food spots on Vinh Khanh Street, District 4.",
        "zh" => $"欢迎来到{name}。这是荣庆美食街第四区的著名美食地点之一。",
        _    => $"Chào mừng bạn đến {name}. Đây là một trong những địa điểm ẩm thực nổi tiếng tại phố Vĩnh Khánh, Quận 4."
    };

    public static string AlertOk => _lang switch { "en" => "OK", "zh" => "确定", _ => "OK" };
    public static string AlertTitle => _lang switch { "en" => "Notice", "zh" => "提示", _ => "Thông báo" };
}

// ──────────────────────────────────────────────
//  DETAIL PAGE
// ──────────────────────────────────────────────
public partial class DetailPage : ContentPage
{
    // ⚠️ ĐỔI IP NÀY:
    //   - Android Emulator  → "http://10.0.2.2:5118"
    //   - Thiết bị thật     → "http://192.168.x.x:5118"  (IP máy tính của bạn)
    //   - Đã deploy lên server → URL thật
    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    {
        Timeout = TimeSpan.FromSeconds(10)   // tránh chờ vô tận
    };

    private PoiDetailDto? _poi;
    private readonly PoiDto _poiBasic;
    private string _lang = "vi";
    private bool _audioBridgeReady;
    private bool _isAudioPlaying;
    private bool _isDraggingSlider;
    private bool _isUpdatingSlider;
    private bool _playWhenReady;
    private double _audioDurationSeconds;
    private string? _audioSourceUrl;

    public DetailPage(PoiDto poi)
    {
        InitializeComponent();
        _poiBasic = poi;
        InitializeAudioBridge();

        // Xác định ngôn ngữ một lần khi tạo page
        _lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        AppStrings.SetLang(_lang);

        // Áp dụng chuỗi UI ngay (trước khi API trả về)
        ApplyLocalizedLabels();

        LoadDetail(poi.Id, poi.TenPOI);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_audioBridgeReady)
            ExecuteAudioScript("stopAudio();");
    }

    // Đặt text cho các label tĩnh theo ngôn ngữ
    private void ApplyLocalizedLabels()
    {
        LblTitle.Text            = _poiBasic.TenPOI;
        LblSectionGioiThieu.Text = AppStrings.SectionIntro;
        LblSectionThucDon.Text   = AppStrings.SectionMenu;
        BtnNghe.Text             = AppStrings.BtnListen;
        BtnMap.Text              = AppStrings.BtnDirection;
    }

    private void InitializeAudioBridge()
    {
        const string audioHtml = """
<!DOCTYPE html>
<html>
<body style="margin:0;background:transparent;">
<audio id="player" preload="metadata"></audio>
<script>
const player = document.getElementById('player');

function emitState() {
  const current = Number.isFinite(player.currentTime) ? player.currentTime : 0;
  const duration = Number.isFinite(player.duration) ? player.duration : 0;
  const paused = player.paused ? '1' : '0';
  window.location.href = `audiostate://${current.toFixed(2)}|${duration.toFixed(2)}|${paused}`;
}

function setSource(url) {
  player.src = url || '';
  player.load();
  emitState();
}

function playAudio() { player.play(); }
function pauseAudio() { player.pause(); }
function stopAudio() {
  player.pause();
  player.currentTime = 0;
  emitState();
}
function seekAudio(seconds) {
  player.currentTime = seconds || 0;
  emitState();
}

player.addEventListener('loadedmetadata', emitState);
player.addEventListener('timeupdate', emitState);
player.addEventListener('play', emitState);
player.addEventListener('pause', emitState);
player.addEventListener('ended', emitState);
</script>
</body>
</html>
""";

        AudioWebView.Source = new HtmlWebViewSource { Html = audioHtml };
    }

    private void ConfigureAudioPlayer()
    {
        _audioSourceUrl = _poi?.FileAudio;
        _playWhenReady = false;
        AudioControls.IsVisible = !string.IsNullOrWhiteSpace(_audioSourceUrl);
        BtnPlayPauseAudio.Text = "Play audio";
        BtnStopAudio.IsEnabled = !string.IsNullOrWhiteSpace(_audioSourceUrl);
        ResetAudioProgress();

        if (_audioBridgeReady && !string.IsNullOrWhiteSpace(_audioSourceUrl))
            ExecuteAudioScript($"setSource({JsonSerializer.Serialize(_audioSourceUrl)});");
    }

    private void ResetAudioProgress()
    {
        _audioDurationSeconds = 0;
        _isAudioPlaying = false;
        _isUpdatingSlider = true;
        AudioProgressSlider.Maximum = 1;
        AudioProgressSlider.Value = 0;
        _isUpdatingSlider = false;
        LblAudioCurrent.Text = "0:00";
        LblAudioDuration.Text = "0:00";
        BtnPlayPauseAudio.Text = "Play audio";
    }

    private void ExecuteAudioScript(string script)
    {
        if (!_audioBridgeReady)
            return;

        AudioWebView.Eval(script);
    }

    private static string FormatTime(double totalSeconds)
    {
        if (totalSeconds < 0 || double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds))
            totalSeconds = 0;

        var time = TimeSpan.FromSeconds(totalSeconds);
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
    }

    private async void LoadDetail(Guid poiId, string tenPoi)
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;

        try
        {
            string apiBaseUrl = await AppConfig.EnsureApiBaseUrlAsync(_http);
            string url = $"{apiBaseUrl}/api/poi/{poiId}?lang={_lang}";
            Console.WriteLine($"[DetailPage] GET {url}");

            _poi = await _http.GetFromJsonAsync<PoiDetailDto>(url);

            if (_poi == null) throw new Exception("API trả về null");

            // Ảnh bìa
            if (!string.IsNullOrEmpty(_poi.AnhDaiDien))
                ImgCover.Source = ImageSource.FromUri(new Uri(AppConfig.ResolveImageUrl(_poi.AnhDaiDien)));

            LblTitle.Text    = _poi.TenPOI;
            LblTen.Text      = _poi.TenPOI;
            LblDiaChi.Text   = "📍 " + (_poi.DiaChi ?? AppStrings.DefaultAddress);
            LblSDT.Text      = string.IsNullOrEmpty(_poi.SDT) ? "" : "📞 " + _poi.SDT;
            LblThuyetMinh.Text = string.IsNullOrEmpty(_poi.NoiDungThuyetMinh)
                ? AppStrings.NoAudio
                : _poi.NoiDungThuyetMinh;

            ConfigureAudioPlayer();
            RenderMenu(_poi.MonAns);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DetailPage] Lỗi: {ex.GetType().Name} — {ex.Message}");
            UseFallback(tenPoi);
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private void UseFallback(string tenPoi)
    {
        var fallbackImg = AppConfig.ResolveImageUrl(
            _poiBasic.AnhDaiDien
            ?? "https://images.unsplash.com/photo-1555396273-367ea4eb4db5?w=800");

        ImgCover.Source    = ImageSource.FromUri(new Uri(fallbackImg));
        LblTitle.Text      = tenPoi;
        LblTen.Text        = tenPoi;
        LblDiaChi.Text     = "📍 " + (_poiBasic.DiaChi ?? AppStrings.DefaultAddress);
        LblSDT.Text        = string.IsNullOrEmpty(_poiBasic.SDT) ? "" : "📞 " + _poiBasic.SDT;
        LblThuyetMinh.Text = AppStrings.FallbackIntro(tenPoi);
        AudioControls.IsVisible = false;
        ResetAudioProgress();

        // Không render menu giả — ẩn section đi cho sạch
        SectionMenu.IsVisible = false;
    }

    // ──────────────────────────────────────────
    //  RENDER MENU
    // ──────────────────────────────────────────
    private void RenderMenu(List<MonAnDto> monAns)
    {
        MenuContainer.Children.Clear();

        if (monAns == null || monAns.Count == 0)
        {
            SectionMenu.IsVisible = false;
            return;
        }

        SectionMenu.IsVisible = true;
        var groups = monAns.GroupBy(m => m.PhanLoai ?? AppStrings.NoMenu);

        foreach (var group in groups)
        {
            // Header nhóm
            MenuContainer.Children.Add(new Label
            {
                Text           = group.Key,
                FontSize       = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor      = Color.FromArgb("#e67e22"),
                Margin         = new Thickness(0, 8, 0, 4)
            });

            foreach (var mon in group)
            {
                var card = new Border
                {
                    BackgroundColor = Colors.White,
                    Stroke          = Color.FromArgb("#EEEEEE"),
                    StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                                      { CornerRadius = 12 },
                    Padding = 0,
                    Shadow  = new Shadow
                    {
                        Brush  = Brush.Black,
                        Offset = new Point(0, 2),
                        Radius = 6,
                        Opacity = 0.06f
                    }
                };

                bool hasImg = !string.IsNullOrEmpty(mon.HinhAnh);

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = hasImg ? 90 : 0 },
                        new ColumnDefinition { Width = GridLength.Star }
                    }
                };

                if (hasImg)
                {
                    var img = new Image
                    {
                        Source       = ImageSource.FromUri(new Uri(AppConfig.ResolveImageUrl(mon.HinhAnh))),
                        Aspect       = Aspect.AspectFill,
                        HeightRequest = 90,
                        WidthRequest  = 90
                    };
                    img.Clip = new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(
                        new CornerRadius(12, 0, 12, 0),
                        new Rect(0, 0, 90, 90));
                    Grid.SetColumn(img, 0);
                    grid.Children.Add(img);
                }

                var info = new VerticalStackLayout
                {
                    Padding = new Thickness(12, 10),
                    Spacing = 4
                };

                info.Children.Add(new Label
                {
                    Text           = mon.TenMonAn,
                    FontSize       = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor      = Colors.Black
                });

                if (!string.IsNullOrEmpty(mon.MoTa))
                    info.Children.Add(new Label
                    {
                        Text          = mon.MoTa,
                        FontSize      = 12,
                        TextColor     = Color.FromArgb("#888"),
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines      = 2
                    });

                if (mon.DonGia > 0)
                    info.Children.Add(new Label
                    {
                        Text           = $"{mon.DonGia:N0}đ",
                        FontSize       = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor      = Color.FromArgb("#e67e22")
                    });

                Grid.SetColumn(info, 1);
                grid.Children.Add(info);
                card.Content = grid;
                MenuContainer.Children.Add(card);
            }
        }
    }

    // ──────────────────────────────────────────
    //  NÚT NGHE THUYẾT MINH
    // ──────────────────────────────────────────
    private async void OnNgheClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_poi?.FileAudio))
        {
            AudioControls.IsVisible = true;
            if (!_audioBridgeReady)
            {
                _playWhenReady = true;
                return;
            }

            ExecuteAudioScript("playAudio();");
            return;
        }

        string text = _poi?.NoiDungThuyetMinh ?? "";

        if (string.IsNullOrEmpty(text))
        {
            await DisplayAlertAsync(AppStrings.AlertTitle, AppStrings.NoAudio, AppStrings.AlertOk);
            return;
        }

        BtnNghe.Text            = AppStrings.BtnListening;
        BtnNghe.BackgroundColor = Colors.DarkOrange;
        BtnNghe.IsEnabled       = false;

        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            // Chọn locale đúng ngôn ngữ
            var locale = _lang switch
            {
                "zh" => locales.FirstOrDefault(l => l.Language.StartsWith("zh"))
                        ?? locales.FirstOrDefault(),
                "en" => locales.FirstOrDefault(l => l.Language.StartsWith("en"))
                        ?? locales.FirstOrDefault(),
                _    => locales.FirstOrDefault(l => l.Language.StartsWith("vi"))
                        ?? locales.FirstOrDefault()
            };

            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Locale = locale });
        }
        finally
        {
            BtnNghe.Text            = AppStrings.BtnListen;
            BtnNghe.BackgroundColor = Color.FromArgb("#4CAF50");
            BtnNghe.IsEnabled       = true;
        }
    }

    // ──────────────────────────────────────────
    //  NÚT CHỈ ĐƯỜNG
    // ──────────────────────────────────────────
    private void OnAudioWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _audioBridgeReady = true;

        if (!string.IsNullOrWhiteSpace(_audioSourceUrl))
            ExecuteAudioScript($"setSource({JsonSerializer.Serialize(_audioSourceUrl)});");

        if (_playWhenReady)
        {
            _playWhenReady = false;
            ExecuteAudioScript("playAudio();");
        }
    }

    private void OnAudioWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("audiostate://", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        var payload = e.Url.Replace("audiostate://", "");
        var parts = payload.Split('|');
        if (parts.Length < 3)
            return;

        if (!double.TryParse(parts[0], CultureInfo.InvariantCulture, out var current))
            current = 0;

        if (!double.TryParse(parts[1], CultureInfo.InvariantCulture, out var duration))
            duration = 0;

        _isAudioPlaying = parts[2] == "0";
        _audioDurationSeconds = duration;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            BtnPlayPauseAudio.Text = _isAudioPlaying ? "Pause" : "Play audio";
            BtnNghe.Text = _isAudioPlaying ? "⏸ Đang phát audio" : AppStrings.BtnListen;
            LblAudioCurrent.Text = FormatTime(current);
            LblAudioDuration.Text = FormatTime(duration);

            if (_isDraggingSlider)
                return;

            _isUpdatingSlider = true;
            AudioProgressSlider.Maximum = duration <= 0 ? 1 : duration;
            AudioProgressSlider.Value = Math.Min(current, AudioProgressSlider.Maximum);
            _isUpdatingSlider = false;
        });
    }

    private void OnPlayPauseClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_audioSourceUrl))
            return;

        ExecuteAudioScript(_isAudioPlaying ? "pauseAudio();" : "playAudio();");
    }

    private void OnStopAudioClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_audioSourceUrl))
            return;

        ExecuteAudioScript("stopAudio();");
    }

    private void OnAudioSliderDragStarted(object? sender, EventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void OnAudioSliderDragCompleted(object? sender, EventArgs e)
    {
        _isDraggingSlider = false;

        if (string.IsNullOrWhiteSpace(_audioSourceUrl))
            return;

        ExecuteAudioScript($"seekAudio({AudioProgressSlider.Value.ToString(CultureInfo.InvariantCulture)});");
    }

    private void OnAudioSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isUpdatingSlider || !_isDraggingSlider)
            return;

        LblAudioCurrent.Text = FormatTime(e.NewValue);
    }

    private async void OnMapClicked(object? sender, EventArgs e)
    {
        double lat = _poi?.ViDo  ?? _poiBasic.ViDo;
        double lng = _poi?.KinhDo ?? _poiBasic.KinhDo;

        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lngStr = lng.ToString(CultureInfo.InvariantCulture);

        await Browser.Default.OpenAsync(
            $"https://maps.google.com/?q={latStr},{lngStr}",
            BrowserLaunchMode.External);
    }
}
