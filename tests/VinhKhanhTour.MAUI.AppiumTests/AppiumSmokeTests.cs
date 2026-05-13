using System.Net.Http.Json;

namespace VinhKhanhTour.MAUI.AppiumTests;

public sealed class AppiumSmokeTests
{
    [Test]
    public async Task AppiumServer_IsReachable_WhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_APPIUM_TESTS"), "1", StringComparison.Ordinal))
        {
            Assert.Ignore("Set RUN_APPIUM_TESTS=1 after starting an emulator/device and Appium server.");
        }

        var serverUrl = Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://127.0.0.1:4723";
        using var http = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        var status = await http.GetFromJsonAsync<AppiumStatus>("status");

        Assert.That(status, Is.Not.Null);
        Assert.That(status!.Value, Is.Not.Null);
        Assert.That(status.Value.Ready, Is.True, "Appium server is reachable but not ready.");
    }

    private sealed class AppiumStatus
    {
        public AppiumStatusValue? Value { get; set; }
    }

    private sealed class AppiumStatusValue
    {
        public bool Ready { get; set; }
    }
}
