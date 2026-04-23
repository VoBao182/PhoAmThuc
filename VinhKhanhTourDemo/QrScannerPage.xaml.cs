using ZXing.Net.Maui;

namespace VinhKhanhTourDemo;

public partial class QrScannerPage : ContentPage
{
    private readonly Func<string, Task> _onCodeDetected;
    private bool _handled;

    public QrScannerPage(Func<string, Task> onCodeDetected)
    {
        InitializeComponent();
        _onCodeDetected = onCodeDetected;

        CameraBarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false,
            TryHarder = true,
            TryInverted = true
        };
    }

    protected override void OnDisappearing()
    {
        CameraBarcodeReader.IsDetecting = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_handled)
            return;

        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return;

        _handled = true;
        CameraBarcodeReader.IsDetecting = false;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            LblStatus.Text = "Da quet ma, dang khoi phuc...";
            await _onCodeDetected(value);

            if (Navigation.ModalStack.LastOrDefault() == this)
                await Navigation.PopModalAsync();
        });
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        CameraBarcodeReader.IsDetecting = false;
        await Navigation.PopModalAsync();
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        CameraBarcodeReader.IsTorchOn = !CameraBarcodeReader.IsTorchOn;
    }

    private void OnFlipCameraClicked(object? sender, EventArgs e)
    {
        CameraBarcodeReader.CameraLocation = CameraBarcodeReader.CameraLocation == CameraLocation.Rear
            ? CameraLocation.Front
            : CameraLocation.Rear;
    }
}
