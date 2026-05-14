using VinhKhanhTourDemo;

namespace VinhKhanhTour.MAUI.AppiumTests;

public sealed class PoiPlaybackLogicTests
{
    private static readonly UserGeoLocation UserLocation = new(10.758900, 106.701800);

    [Test]
    public void Select_ReturnsNull_WhenUserIsOutsideEveryPoiRadius()
    {
        var poi = CreatePoi("Outside", metersNorth: 100, radiusMeters: 30, priority: 1);

        var selected = PoiGeofenceSelector.Select([poi], UserLocation);

        Assert.That(selected, Is.Null);
    }

    [Test]
    public void Select_UsesPriorityBeforeDistance_WhenMultiplePoisContainUser()
    {
        var priorityPoi = CreatePoi("Priority wins", metersNorth: 25, radiusMeters: 50, priority: 1);
        var closerPoi = CreatePoi("Closer but lower priority", metersNorth: 0, radiusMeters: 50, priority: 2);

        var selected = PoiGeofenceSelector.Select([closerPoi, priorityPoi], UserLocation);

        Assert.That(selected?.Poi.Id, Is.EqualTo(priorityPoi.Id));
    }

    [Test]
    public void Select_UsesNearestPoi_WhenPriorityIsEqual()
    {
        var farPoi = CreatePoi("Far", metersNorth: 20, radiusMeters: 50, priority: 1);
        var nearPoi = CreatePoi("Near", metersNorth: 5, radiusMeters: 50, priority: 1);

        var selected = PoiGeofenceSelector.Select([farPoi, nearPoi], UserLocation);

        Assert.That(selected?.Poi.Id, Is.EqualTo(nearPoi.Id));
    }

    [Test]
    public void Select_UsesAlphabeticalName_WhenPriorityAndDistanceAreEqual()
    {
        var second = CreatePoi("B POI", metersNorth: 0, radiusMeters: 50, priority: 1);
        var first = CreatePoi("A POI", metersNorth: 0, radiusMeters: 50, priority: 1);

        var selected = PoiGeofenceSelector.Select([second, first], UserLocation);

        Assert.That(selected?.Poi.Id, Is.EqualTo(first.Id));
    }

    [Test]
    public void Select_KeepsCurrentPoi_WhenSamePriorityAndWithinSwitchBuffer()
    {
        var currentPoi = CreatePoi("Current", metersNorth: 4, radiusMeters: 50, priority: 1);
        var slightlyCloserPoi = CreatePoi("Closer", metersNorth: 0, radiusMeters: 50, priority: 1);

        var selected = PoiGeofenceSelector.Select(
            [slightlyCloserPoi, currentPoi],
            UserLocation,
            currentPoi.Id,
            switchDistanceBufferMeters: 5);

        Assert.That(selected?.Poi.Id, Is.EqualTo(currentPoi.Id));
    }

    [Test]
    public void Select_SwitchesPoi_WhenCurrentPoiIsOutsideSwitchBuffer()
    {
        var currentPoi = CreatePoi("Current", metersNorth: 15, radiusMeters: 50, priority: 1);
        var closerPoi = CreatePoi("Closer", metersNorth: 0, radiusMeters: 50, priority: 1);

        var selected = PoiGeofenceSelector.Select(
            [currentPoi, closerPoi],
            UserLocation,
            currentPoi.Id,
            switchDistanceBufferMeters: 5);

        Assert.That(selected?.Poi.Id, Is.EqualTo(closerPoi.Id));
    }

    [Test]
    public void PlaybackQueue_RejectsDuplicateQueuedPoi()
    {
        var queue = new PoiPlaybackQueue<string>();
        var poiId = Guid.NewGuid();

        var first = queue.Enqueue(poiId, "first");
        var duplicate = queue.Enqueue(poiId, "duplicate");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(duplicate, Is.False);
            Assert.That(queue.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void PlaybackQueue_RejectsPoiThatIsAlreadyPlaying()
    {
        var queue = new PoiPlaybackQueue<string>();
        var poiId = Guid.NewGuid();

        queue.SetPlaying(poiId);
        var accepted = queue.Enqueue(poiId, "same poi");

        Assert.That(accepted, Is.False);
    }

    [Test]
    public void PlaybackQueue_DequeuesInFirstInFirstOutOrder()
    {
        var queue = new PoiPlaybackQueue<string>();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        queue.Enqueue(firstId, "first");
        queue.Enqueue(secondId, "second");

        var firstDequeued = queue.TryDequeue(out var dequeuedFirstId, out var firstValue);
        var secondDequeued = queue.TryDequeue(out var dequeuedSecondId, out var secondValue);

        Assert.Multiple(() =>
        {
            Assert.That(firstDequeued, Is.True);
            Assert.That(secondDequeued, Is.True);
            Assert.That(dequeuedFirstId, Is.EqualTo(firstId));
            Assert.That(firstValue, Is.EqualTo("first"));
            Assert.That(dequeuedSecondId, Is.EqualTo(secondId));
            Assert.That(secondValue, Is.EqualTo("second"));
        });
    }

    private static PoiPlaybackItem CreatePoi(
        string name,
        double metersNorth,
        int radiusMeters,
        int priority)
    {
        const double metersPerLatitudeDegree = 111_320.0;
        return new PoiPlaybackItem(
            Guid.NewGuid(),
            name,
            UserLocation.Latitude + (metersNorth / metersPerLatitudeDegree),
            UserLocation.Longitude,
            radiusMeters,
            priority);
    }
}
