using Microsoft.Maui.Controls.Shapes;

namespace VinhKhanhTourDemo
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                var navPage = new NavigationPage(new LaunchPage())
                {
                    BarBackgroundColor = Color.FromArgb("#FF5722"),
                    BarTextColor = Colors.White
                };
                return new Window(navPage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup] CreateWindow failed: {ex}");
                return new Window(new ContentPage
                {
                    BackgroundColor = Color.FromArgb("#F7F2EB"),
                    Content = new ScrollView
                    {
                        Content = new VerticalStackLayout
                        {
                            Padding = new Thickness(24, 56, 24, 32),
                            Spacing = 14,
                            Children =
                            {
                                new Label
                                {
                                    Text = AppText.T("Không thể mở app", "Unable to open app", "无法打开应用"),
                                    FontSize = 28,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#C2410C")
                                },
                                new Label
                                {
                                    Text = AppText.T(
                                        "App đã gặp lỗi ngay khi khởi động. Màn hình này giữ app mở để mình tiếp tục truy vết lỗi.",
                                        "The app hit an error during startup. This screen keeps it open so the issue can be traced.",
                                        "应用启动时出错。此页面会保持应用打开，便于继续排查。"),
                                    FontSize = 15,
                                    TextColor = Color.FromArgb("#374151")
                                },
                                new Label
                                {
                                    Text = ex.Message,
                                    FontSize = 13,
                                    TextColor = Color.FromArgb("#6B7280")
                                }
                            }
                        }
                    }
                });
            }
        }
    }
}
