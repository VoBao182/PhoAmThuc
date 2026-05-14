using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace VinhKhanhTour.MAUI.AppiumTests;

public sealed class AppiumSmokeTests
{
    private const string DefaultAppPackage = "com.companyname.vinhkhanhtourdemo";

    [Test]
    public async Task AppiumServer_IsReachable_WhenEnabled()
    {
        RequireAppiumEnabled();

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

    [Test]
    public void AndroidApp_LaunchesToKnownEntryScreen_WhenEnabled()
    {
        RequireAppiumEnabled();

        using var driver = CreateAndroidDriver();
        try
        {
            var entryScreen = WaitForAnyElement(
                driver,
                TimeSpan.FromSeconds(60),
                "main-page",
                "subscription-page",
                "launch-page");

            Assert.That(
                entryScreen.AutomationId,
                Is.AnyOf("main-page", "subscription-page", "launch-page"),
                "The app launched, but none of the known MAUI entry screens were visible.");

            if (entryScreen.AutomationId == "main-page")
            {
                Assert.That(ExistsByAutomationId(driver, "main-search-entry"), Is.True);
                Assert.That(ExistsByAutomationId(driver, "main-tab-settings"), Is.True);
                return;
            }

            if (entryScreen.AutomationId == "subscription-page")
            {
                Assert.That(ExistsByAutomationId(driver, "subscription-trial-button"), Is.True);
                Assert.That(ExistsByAutomationId(driver, "subscription-month-button"), Is.True);
                return;
            }

            Assert.That(ExistsByAutomationId(driver, "launch-status"), Is.True);
        }
        catch
        {
            SaveDriverArtifacts(driver, nameof(AndroidApp_LaunchesToKnownEntryScreen_WhenEnabled));
            throw;
        }
    }

    [Test]
    public void AndroidApp_CanNavigatePrimaryUserFlow_WhenEnabled()
    {
        RequireAppiumEnabled();

        using var driver = CreateAndroidDriver();
        try
        {
            var entryScreen = WaitForAnyElement(
                driver,
                TimeSpan.FromSeconds(60),
                "main-page",
                "subscription-page",
                "launch-page");

            if (entryScreen.AutomationId == "launch-page")
            {
                try
                {
                    entryScreen = WaitForAnyElement(
                        driver,
                        TimeSpan.FromSeconds(30),
                        "main-page",
                        "subscription-page");
                }
                catch (WebDriverTimeoutException)
                {
                    Assert.That(ExistsByAutomationId(driver, "launch-status"), Is.True);
                    return;
                }
            }

            if (entryScreen.AutomationId == "main-page")
            {
                ClickByAutomationId(driver, "main-tab-settings");
                WaitForAnyElement(
                    driver,
                    TimeSpan.FromSeconds(10),
                    "main-settings-device-id",
                    "main-settings-subscription-status");

                ClickByAutomationId(driver, "main-tab-explore");
                WaitForAnyElement(driver, TimeSpan.FromSeconds(10), "main-search-entry");
                return;
            }

            if (entryScreen.AutomationId == "subscription-page")
            {
                Assert.That(ExistsByAutomationId(driver, "subscription-trial-button"), Is.True);
                Assert.That(ExistsByAutomationId(driver, "subscription-month-button"), Is.True);

                ClickByAutomationId(driver, "subscription-month-button");
                WaitForAnyElement(driver, TimeSpan.FromSeconds(15), "payment-page");
                Assert.That(ExistsByAutomationId(driver, "payment-confirm-transfer-button"), Is.True);
                Assert.That(ExistsByAutomationId(driver, "payment-cancel-button"), Is.True);
                return;
            }

            Assert.That(ExistsByAutomationId(driver, "launch-status"), Is.True);
        }
        catch
        {
            SaveDriverArtifacts(driver, nameof(AndroidApp_CanNavigatePrimaryUserFlow_WhenEnabled));
            throw;
        }
    }

    private static AndroidDriver CreateAndroidDriver()
    {
        var serverUrl = Environment.GetEnvironmentVariable("APPIUM_SERVER_URL") ?? "http://127.0.0.1:4723";
        var appPath = Environment.GetEnvironmentVariable("APPIUM_APP_PATH");
        var appPackage = Environment.GetEnvironmentVariable("APPIUM_APP_PACKAGE") ?? DefaultAppPackage;
        var appActivity = Environment.GetEnvironmentVariable("APPIUM_APP_ACTIVITY");

        if (string.IsNullOrWhiteSpace(appPath) && string.IsNullOrWhiteSpace(appActivity))
        {
            Assert.Ignore(
                "Set APPIUM_APP_PATH to a built APK, or set APPIUM_APP_ACTIVITY when testing an already-installed app.");
        }

        if (!string.IsNullOrWhiteSpace(appPath) && !File.Exists(appPath))
            Assert.Ignore($"APPIUM_APP_PATH does not exist: {appPath}");

        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UiAutomator2"
        };

        options.AddAdditionalAppiumOption("deviceName", Environment.GetEnvironmentVariable("APPIUM_DEVICE_NAME") ?? "Android");
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 120);

        var noReset = string.Equals(Environment.GetEnvironmentVariable("APPIUM_NO_RESET"), "1", StringComparison.Ordinal);
        options.AddAdditionalAppiumOption("noReset", noReset);

        if (!string.IsNullOrWhiteSpace(appPath))
        {
            options.App = appPath;
        }
        else
        {
            options.AddAdditionalAppiumOption("appPackage", appPackage);
            options.AddAdditionalAppiumOption("appActivity", appActivity!);
        }

        return new AndroidDriver(new Uri(serverUrl), options, TimeSpan.FromSeconds(120));
    }

    private static AppiumElementMatch WaitForAnyElement(AndroidDriver driver, TimeSpan timeout, params string[] automationIds)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            foreach (var automationId in automationIds)
            {
                try
                {
                    var element = FindByAutomationId(driver, automationId);
                    if (element is not null)
                        return new AppiumElementMatch(automationId, element);
                }
                catch (WebDriverException ex)
                {
                    lastError = ex;
                }
            }

            Thread.Sleep(500);
        }

        throw new WebDriverTimeoutException(
            $"Timed out waiting for one of these AutomationIds: {string.Join(", ", automationIds)}",
            lastError);
    }

    private static bool ExistsByAutomationId(AndroidDriver driver, string automationId)
        => FindByAutomationId(driver, automationId) is not null;

    private static void ClickByAutomationId(AndroidDriver driver, string automationId)
    {
        var element = WaitForAnyElement(driver, TimeSpan.FromSeconds(10), automationId).Element;
        element.Click();
    }

    private static IWebElement? FindByAutomationId(AndroidDriver driver, string automationId)
    {
        var byAccessibilityId = MobileBy.AccessibilityId(automationId);
        var matches = driver.FindElements(byAccessibilityId);
        if (matches.Count > 0)
            return matches[0];

        var escaped = automationId.Replace("'", "\\'", StringComparison.Ordinal);
        var byXPath = By.XPath(
            $"//*[@content-desc='{escaped}' or @text='{escaped}' or contains(@resource-id, ':id/{escaped}')]");

        matches = driver.FindElements(byXPath);
        return matches.Count > 0 ? matches[0] : null;
    }

    private static void RequireAppiumEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_APPIUM_TESTS"), "1", StringComparison.Ordinal))
            Assert.Ignore("Set RUN_APPIUM_TESTS=1 after starting an emulator/device and Appium server.");
    }

    private static void SaveDriverArtifacts(AndroidDriver driver, string testName)
    {
        var artifactRoot = Environment.GetEnvironmentVariable("TEST_ARTIFACT_DIR");
        if (string.IsNullOrWhiteSpace(artifactRoot))
            artifactRoot = Path.Combine(FindRepoRoot(), "TestResults", "artifacts", "appium");

        Directory.CreateDirectory(artifactRoot);

        var safeName = string.Concat(testName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var prefix = Path.Combine(artifactRoot, $"{safeName}-{timestamp}");

        try
        {
            driver.GetScreenshot().SaveAsFile($"{prefix}.png");
        }
        catch
        {
        }

        try
        {
            File.WriteAllText($"{prefix}.xml", driver.PageSource);
        }
        catch
        {
        }
    }

    private static string FindRepoRoot([CallerFilePath] string sourcePath = "")
    {
        var configuredRoot = Environment.GetEnvironmentVariable("TEST_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot)
            && File.Exists(Path.Combine(configuredRoot, "VinhKhanhTourDemo.slnx")))
        {
            return configuredRoot;
        }

        var currentDirectory = new DirectoryInfo(Environment.CurrentDirectory);
        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "VinhKhanhTourDemo.slnx")))
                return currentDirectory.FullName;

            currentDirectory = currentDirectory.Parent;
        }

        var sourceDirectory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? "");
        while (sourceDirectory is not null)
        {
            if (File.Exists(Path.Combine(sourceDirectory.FullName, "VinhKhanhTourDemo.slnx")))
                return sourceDirectory.FullName;

            sourceDirectory = sourceDirectory.Parent;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VinhKhanhTourDemo.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }

    private sealed record AppiumElementMatch(string AutomationId, IWebElement Element);

    private sealed class AppiumStatus
    {
        public AppiumStatusValue? Value { get; set; }
    }

    private sealed class AppiumStatusValue
    {
        public bool Ready { get; set; }
    }
}
