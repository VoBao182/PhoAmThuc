using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;

namespace VinhKhanhTourDemo.Platforms.Android
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges =
            ConfigChanges.ScreenSize | ConfigChanges.Orientation |
            ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping(
                "EnableGeolocation",
                (handler, view) =>
                {
                    if (handler.PlatformView is not global::Android.Webkit.WebView webView) return;
                    webView.Settings.JavaScriptEnabled = true;
                    webView.Settings.SetGeolocationEnabled(true);
                    webView.Settings.SetGeolocationDatabasePath(
                        global::Android.App.Application.Context.FilesDir?.Path);
                    webView.SetWebChromeClient(new GeoWebChromeClient());
                });
        }
    }

    public class GeoWebChromeClient : global::Android.Webkit.WebChromeClient
    {
        public override void OnGeolocationPermissionsShowPrompt(
            string? origin,
            global::Android.Webkit.GeolocationPermissions.ICallback? callback)
        {
            callback?.Invoke(origin, true, false);
        }
    }
}
