using System.Diagnostics;
using Microsoft.Playwright;

namespace VinhKhanhTour.CMS.E2ETests;

public sealed class CmsSmokeTests : IAsyncLifetime
{
    private Process? _cmsProcess;

    [Fact]
    public async Task CmsHome_OpensInChromium()
    {
        var baseUrl = Environment.GetEnvironmentVariable("CMS_BASE_URL") ?? "http://127.0.0.1:5199";
        await EnsureCmsIsRunningAsync(baseUrl);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        var healthResponse = await page.GotoAsync(
            $"{baseUrl.TrimEnd('/')}/health",
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 10000
            });

        Assert.NotNull(healthResponse);
        Assert.True(healthResponse.Ok, $"CMS health endpoint returned HTTP {healthResponse.Status}.");

        await page.GotoAsync(
            $"{baseUrl.TrimEnd('/')}/Privacy",
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

        var bodyText = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions
        {
            Timeout = 10000
        });

        Assert.Contains("Privacy Policy", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_cmsProcess is null || _cmsProcess.HasExited)
            return;

        _cmsProcess.Kill(entireProcessTree: true);
        await _cmsProcess.WaitForExitAsync();
    }

    private async Task EnsureCmsIsRunningAsync(string baseUrl)
    {
        if (await IsHealthyAsync(baseUrl))
            return;

        var repoRoot = FindRepoRoot();
        var cmsProject = Path.Combine(repoRoot, "VinhKhanhTour.CMS", "VinhKhanhTour.CMS.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(cmsProject);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.Environment["ASPNETCORE_URLS"] = baseUrl;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        startInfo.Environment["SUPABASE_CONNECTION_STRING"] =
            "Host=localhost;Port=5432;Database=vinhkhanhtour_test;Username=postgres;Password=postgres;Timeout=1;Command Timeout=1;Pooling=false";
        startInfo.Environment["Logging__LogLevel__Default"] = "Warning";
        startInfo.Environment["Logging__LogLevel__Microsoft.AspNetCore"] = "Warning";
        startInfo.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Warning";

        _cmsProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start CMS process.");

        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (_cmsProcess.HasExited)
                throw new InvalidOperationException("CMS process exited before it became ready.");

            if (await IsHealthyAsync(baseUrl))
                return;

            await Task.Delay(700);
        }

        throw new TimeoutException($"CMS did not become ready at {baseUrl}.");
    }

    private static async Task<bool> IsHealthyAsync(string baseUrl)
    {
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        try
        {
            var response = await http.GetAsync($"{baseUrl.TrimEnd('/')}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VinhKhanhTourDemo.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }
}
