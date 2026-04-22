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
                                    Text = "Không thể mở app",
                                    FontSize = 28,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#C2410C")
                                },
                                new Label
                                {
                                    Text = "App đã gặp lỗi ngay khi khởi động. Màn hình này giữ app mở để mình tiếp tục truy vết lỗi.",
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
