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
            Page rootPage = AppConfig.ShouldBlockPublishedAndroidApp
                ? BuildMissingConfigurationPage()
                : new MainPage();

            var navPage = new NavigationPage(rootPage)
            {
                BarBackgroundColor = Color.FromArgb("#FF5722"),
                BarTextColor = Colors.White
            };
            return new Window(navPage);
        }

        private static Page BuildMissingConfigurationPage()
        {
            return new ContentPage
            {
                BackgroundColor = Color.FromArgb("#F7F2EB"),
                Content = new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = new Thickness(24, 60, 24, 32),
                        Spacing = 16,
                        Children =
                        {
                            new Label
                            {
                                Text = "Vinh Khanh Tour",
                                FontSize = 26,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#1F2933"),
                                HorizontalOptions = LayoutOptions.Center
                            },
                            new Border
                            {
                                BackgroundColor = Colors.White,
                                Stroke = Color.FromArgb("#E5E7EB"),
                                Padding = new Thickness(18, 16),
                                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                                Content = new VerticalStackLayout
                                {
                                    Spacing = 12,
                                    Children =
                                    {
                                        new Label
                                        {
                                            Text = "Chua cau hinh ban phat hanh",
                                            FontSize = 18,
                                            FontAttributes = FontAttributes.Bold,
                                            TextColor = Color.FromArgb("#B91C1C")
                                        },
                                        new Label
                                        {
                                            Text = AppConfig.BuildMissingHostedApiMessage(),
                                            FontSize = 14,
                                            TextColor = Color.FromArgb("#374151")
                                        },
                                        new Label
                                        {
                                            Text = "Can cap nhat VinhKhanhTourDemo/AppEndpointOptions.cs voi public API URL, vi du https://api.vinhkhanhtour.vn.",
                                            FontSize = 13,
                                            TextColor = Color.FromArgb("#6B7280")
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
