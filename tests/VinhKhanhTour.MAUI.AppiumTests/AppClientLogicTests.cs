using VinhKhanhTourDemo;

namespace VinhKhanhTour.MAUI.AppiumTests;

public sealed class AppClientLogicTests
{
    [Test]
    public void NormalizeApiBaseUrl_TrimsTrailingSlashAndPath()
    {
        var result = AppClientLogic.NormalizeApiBaseUrl(" https://api.example.com/v1/ ");

        Assert.That(result, Is.EqualTo("https://api.example.com"));
    }

    [Test]
    public void NormalizeApiBaseUrl_UpgradesPublicHttpButKeepsLoopbackHttp()
    {
        Assert.That(
            AppClientLogic.NormalizeApiBaseUrl("http://api.example.com"),
            Is.EqualTo("https://api.example.com"));

        Assert.That(
            AppClientLogic.NormalizeApiBaseUrl("http://localhost:5118/"),
            Is.EqualTo("http://localhost:5118"));

        Assert.That(
            AppClientLogic.NormalizeApiBaseUrl("http://127.0.0.1:5118/"),
            Is.EqualTo("http://127.0.0.1:5118"));
    }

    [Test]
    public void NormalizeApiBaseUrl_RejectsUnsupportedSchemesAndInvalidText()
    {
        Assert.That(AppClientLogic.NormalizeApiBaseUrl("ftp://example.com"), Is.Null);
        Assert.That(AppClientLogic.NormalizeApiBaseUrl("not a url"), Is.Null);
        Assert.That(AppClientLogic.NormalizeApiBaseUrl(""), Is.Null);
    }

    [Test]
    public void ResolveImageUrl_CombinesRelativeUploadWithApiBaseUrl()
    {
        var result = AppClientLogic.ResolveImageUrl("/uploads/cover.png", "https://api.example.com/");

        Assert.That(result, Is.EqualTo("https://api.example.com/uploads/cover.png"));
    }

    [Test]
    public void ResolveImageUrl_RewritesLocalhostImageToResolvedApiHost()
    {
        var result = AppClientLogic.ResolveImageUrl(
            "http://localhost:5118/uploads/cover.png",
            "https://phoamthuc.onrender.com");

        Assert.That(result, Is.EqualTo("https://phoamthuc.onrender.com/uploads/cover.png"));
    }

    [Test]
    public void CalculateRemainingDays_FloorsFutureDaysAndNeverReturnsNegative()
    {
        var now = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc);

        Assert.That(AppClientLogic.CalculateRemainingDays(now.AddDays(3).AddHours(23), now), Is.EqualTo(3));
        Assert.That(AppClientLogic.CalculateRemainingDays(now.AddMinutes(-1), now), Is.EqualTo(0));
    }
}
