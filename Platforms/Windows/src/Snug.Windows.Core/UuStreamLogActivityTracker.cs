namespace Snug.Windows.Core;

/// <summary>
/// Treats append growth in the current GameViewerServer streamer log as a
/// low-confidence activity hint. A locked file is not activity: UU can hold
/// the handle for the whole server lifetime.
/// </summary>
public sealed class UuStreamLogActivityTracker
{
    public static readonly TimeSpan GrowthWindow = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan Freshness = TimeSpan.FromSeconds(2);

    private string? _source;
    private long? _lastLength;
    private DateTimeOffset? _lastSampleAt;
    private DateTimeOffset? _previousGrowthAt;
    private DateTimeOffset? _lastGrowthAt;

    public bool Observe(string source, long length, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(source) || length < 0)
        {
            Reset();
            return false;
        }

        if (!string.Equals(_source, source, StringComparison.OrdinalIgnoreCase))
        {
            Reset();
            _source = source;
            _lastLength = length;
            _lastSampleAt = now;
            return false;
        }

        if (_lastSampleAt is { } previousSample && now < previousSample)
        {
            Reset();
            _source = source;
            _lastLength = length;
            _lastSampleAt = now;
            return false;
        }

        _lastSampleAt = now;
        if (_lastLength is not { } previousLength)
        {
            _lastLength = length;
            return false;
        }

        if (length < previousLength)
        {
            _lastLength = length;
            _previousGrowthAt = null;
            _lastGrowthAt = null;
            return false;
        }

        if (length > previousLength)
        {
            _lastLength = length;
            _previousGrowthAt = _lastGrowthAt;
            _lastGrowthAt = now;
        }

        if (_previousGrowthAt is not { } first
            || _lastGrowthAt is not { } last
            || last - first > GrowthWindow)
            return false;

        var age = now - last;
        return age >= TimeSpan.Zero && age <= Freshness;
    }

    public void Reset()
    {
        _source = null;
        _lastLength = null;
        _lastSampleAt = null;
        _previousGrowthAt = null;
        _lastGrowthAt = null;
    }
}