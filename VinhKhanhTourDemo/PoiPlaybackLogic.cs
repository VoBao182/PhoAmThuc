namespace VinhKhanhTourDemo;

internal readonly record struct UserGeoLocation(double Latitude, double Longitude);

internal sealed record PoiPlaybackItem(
    Guid Id,
    string TenPOI,
    double ViDo,
    double KinhDo,
    int BanKinh,
    int MucUuTien);

internal sealed record PoiSelection(PoiPlaybackItem Poi, double DistanceMeters);

internal static class PoiGeofenceSelector
{
    public static PoiSelection? Select(
        IEnumerable<PoiPlaybackItem> pois,
        UserGeoLocation userLocation,
        Guid? currentPoiId = null,
        double switchDistanceBufferMeters = 5.0)
    {
        var candidates = pois
            .Select(poi => new PoiSelection(
                poi,
                HaversineMeters(userLocation.Latitude, userLocation.Longitude, poi.ViDo, poi.KinhDo)))
            .Where(candidate => candidate.DistanceMeters <= candidate.Poi.BanKinh)
            .OrderBy(candidate => candidate.Poi.MucUuTien)
            .ThenBy(candidate => candidate.DistanceMeters)
            .ThenBy(candidate => candidate.Poi.TenPOI, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var selected = candidates[0];
        if (!currentPoiId.HasValue || currentPoiId.Value == selected.Poi.Id)
            return selected;

        var currentCandidate = candidates.FirstOrDefault(candidate => candidate.Poi.Id == currentPoiId.Value);
        if (currentCandidate is not null
            && currentCandidate.Poi.MucUuTien == selected.Poi.MucUuTien
            && currentCandidate.DistanceMeters <= selected.DistanceMeters + switchDistanceBufferMeters)
        {
            return currentCandidate;
        }

        return selected;
    }

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusMeters = 6_371_000.0;
        static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var sinLat = Math.Sin(dLat / 2);
        var sinLng = Math.Sin(dLng / 2);
        var a = sinLat * sinLat
            + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * sinLng * sinLng;
        var c = 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        return earthRadiusMeters * c;
    }
}

internal sealed class PoiPlaybackQueue<T>
{
    private readonly Queue<QueuedItem> _queue = new();
    private readonly HashSet<Guid> _queuedIds = [];
    private Guid? _playingId;

    public int Count => _queue.Count;

    public bool Enqueue(Guid id, T value)
    {
        if (_playingId == id || _queuedIds.Contains(id))
            return false;

        _queue.Enqueue(new QueuedItem(id, value));
        _queuedIds.Add(id);
        return true;
    }

    public bool TryDequeue(out Guid id, out T? value)
    {
        if (_queue.Count == 0)
        {
            id = Guid.Empty;
            value = default;
            return false;
        }

        var queued = _queue.Dequeue();
        _queuedIds.Remove(queued.Id);
        id = queued.Id;
        value = queued.Value;
        return true;
    }

    public void SetPlaying(Guid id) => _playingId = id;

    public void ClearPlaying(Guid id)
    {
        if (_playingId == id)
            _playingId = null;
    }

    private sealed record QueuedItem(Guid Id, T Value);
}

internal sealed class PoiGeofencePlaybackController
{
    private readonly IReadOnlyList<PoiPlaybackItem> _pois;
    private readonly double _dwellSecondsToConfirm;
    private readonly TimeSpan _cooldown;
    private readonly double _switchDistanceBufferMeters;
    private readonly Dictionary<Guid, DateTime> _lastQueuedAtUtc = [];
    private Guid? _candidatePoiId;
    private DateTime _candidateSinceUtc;
    private Guid? _currentPoiId;

    public PoiGeofencePlaybackController(
        IEnumerable<PoiPlaybackItem> pois,
        double dwellSecondsToConfirm = 5.0,
        TimeSpan? cooldown = null,
        double switchDistanceBufferMeters = 5.0)
    {
        _pois = pois.ToList();
        _dwellSecondsToConfirm = dwellSecondsToConfirm;
        _cooldown = cooldown ?? TimeSpan.FromMinutes(10);
        _switchDistanceBufferMeters = switchDistanceBufferMeters;
    }

    public GeofencePlaybackStep Evaluate(UserGeoLocation userLocation, DateTime nowUtc)
    {
        var selected = PoiGeofenceSelector.Select(
            _pois,
            userLocation,
            _currentPoiId,
            _switchDistanceBufferMeters);

        if (selected is null)
        {
            _candidatePoiId = null;
            _currentPoiId = null;
            return new GeofencePlaybackStep(null, null, false, "outside");
        }

        var selectedId = selected.Poi.Id;
        if (_candidatePoiId != selectedId)
        {
            _candidatePoiId = selectedId;
            _candidateSinceUtc = nowUtc;
            return new GeofencePlaybackStep(selectedId, _currentPoiId, false, "dwell_started");
        }

        var dwell = nowUtc - _candidateSinceUtc;
        if (dwell.TotalSeconds < _dwellSecondsToConfirm)
            return new GeofencePlaybackStep(selectedId, _currentPoiId, false, "dwelling");

        var isNewPoi = _currentPoiId != selectedId;
        _currentPoiId = selectedId;

        var cooledDown = !_lastQueuedAtUtc.TryGetValue(selectedId, out var lastQueued)
            || nowUtc - lastQueued >= _cooldown;

        if (!isNewPoi)
            return new GeofencePlaybackStep(selectedId, _currentPoiId, false, "already_current");

        if (!cooledDown)
            return new GeofencePlaybackStep(selectedId, _currentPoiId, false, "cooldown");

        _lastQueuedAtUtc[selectedId] = nowUtc;
        return new GeofencePlaybackStep(selectedId, _currentPoiId, true, "queued");
    }

    public void ClearCurrentPoi()
    {
        _candidatePoiId = null;
        _currentPoiId = null;
    }
}

internal sealed record GeofencePlaybackStep(
    Guid? SelectedPoiId,
    Guid? CurrentPoiId,
    bool QueuedPlayback,
    string Reason);
