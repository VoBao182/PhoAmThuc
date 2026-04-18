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
                var navPage = new NavigationPage(new MainPage())
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
                                    Text = "Khong the mo app",
                                    FontSize = 28,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#C2410C")
                                },
                                new Label
                                {
                                    Text = "App da gap loi ngay khi khoi dong. Man hinh nay giu app mo de minh tiep tuc truy vet loi.",
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
