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
            var navPage = new NavigationPage(new MainPage())
            {
                BarBackgroundColor = Color.FromArgb("#FF5722"),
                BarTextColor = Colors.White
            };
            return new Window(navPage);
        }
    }
}
