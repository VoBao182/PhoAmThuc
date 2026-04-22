namespace VinhKhanhTourDemo;

public partial class LaunchPage : ContentPage
{
    private bool _started;

    public LaunchPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
            return;

        _started = true;
        Dispatcher.Dispatch(async () => await RouteAsync());
    }

    private async Task RouteAsync()
    {
        BootIndicator.IsVisible = true;
        BootIndicator.IsRunning = true;
        BtnRetry.IsVisible = false;
        LblStatus.Text = "Đang khởi động ứng dụng...";

        try
        {
            await Task.Delay(250);

            Page targetPage = SubscriptionState.IsSubscriptionActive()
                ? new MainPage()
                : new SubscriptionPage(SubscriptionState.HasStoredSubscriptionRecord());

            await Navigation.PushAsync(targetPage, animated: false);
            Navigation.RemovePage(this);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] LaunchPage route failed: {ex}");
            BootIndicator.IsRunning = false;
            BootIndicator.IsVisible = false;
            BtnRetry.IsVisible = true;
            LblStatus.Text = $"Không thể mở ứng dụng.\n{ex.Message}";
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await RouteAsync();
    }
}
