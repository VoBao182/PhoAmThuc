namespace VinhKhanhTourDemo;

public partial class LaunchPage : ContentPage
{
    private bool _started;

    public LaunchPage()
    {
        InitializeComponent();
        ApplyLocalizedUiText();
    }

    private void ApplyLocalizedUiText()
    {
        LblStatus.Text = AppText.T(
            "Đang khởi động ứng dụng...",
            "Starting the app...",
            "正在启动应用...");
        BtnRetry.Text = AppText.T("Thử lại", "Retry", "重试");
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
        LblStatus.Text = AppText.T(
            "Đang khởi động ứng dụng...",
            "Starting the app...",
            "正在启动应用...");

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
            LblStatus.Text = AppText.T(
                $"Không thể mở ứng dụng.\n{ex.Message}",
                $"Unable to open the app.\n{ex.Message}",
                $"无法打开应用。\n{ex.Message}");
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await RouteAsync();
    }
}
