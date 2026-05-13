using System.Xml.Linq;

namespace VinhKhanhTour.MAUI.AppiumTests;

public sealed class AutomationIdContractTests
{
    private static readonly string[] RequiredAutomationIds =
    [
        "launch-page",
        "launch-status",
        "subscription-page",
        "subscription-trial-button",
        "subscription-month-button",
        "payment-page",
        "payment-confirm-transfer-button",
        "payment-status-page",
        "main-page",
        "main-search-entry",
        "main-tab-explore",
        "main-tab-map",
        "main-tab-settings",
        "detail-page",
        "detail-listen-button",
        "qr-scanner-page",
        "qr-scanner-close-button"
    ];

    [Test]
    public void MauiXaml_ContainsRequiredAutomationIds()
    {
        var automationIds = LoadXamlAutomationIds().ToHashSet(StringComparer.Ordinal);

        foreach (var requiredId in RequiredAutomationIds)
            Assert.That(automationIds, Does.Contain(requiredId), $"Missing AutomationId '{requiredId}'.");
    }

    [Test]
    public void MauiXaml_AutomationIdsAreUnique()
    {
        var duplicates = LoadXamlAutomationIds()
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({group.Count()} times)")
            .ToArray();

        Assert.That(duplicates, Is.Empty, $"Duplicate AutomationIds found: {string.Join(", ", duplicates)}");
    }

    private static IEnumerable<string> LoadXamlAutomationIds()
    {
        var appDirectory = Path.Combine(FindRepoRoot(), "VinhKhanhTourDemo");
        foreach (var xamlPath in Directory.EnumerateFiles(appDirectory, "*.xaml", SearchOption.TopDirectoryOnly))
        {
            var document = XDocument.Load(xamlPath);
            if (document.Root is null)
                continue;

            foreach (var attribute in document.Root.DescendantsAndSelf().Attributes("AutomationId"))
                yield return attribute.Value;
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
