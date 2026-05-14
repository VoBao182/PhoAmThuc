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
