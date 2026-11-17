namespace Snug.Windows.Core;

/// <summary>
/// Builds a trustworthy capture inventory only when ETW was already active
/// before the UU server generation started. Late attachment remains unknown.
/// </summary>
public sealed class UuCaptureMonitorState(DateTimeOffset traceStartedAt)
{
    private readonly UuCaptureLifecycle _lifecycle = new();
    private readonly Dictionary<(ulong Log, ulong Index), ulong> _contexts = [];
    private UuServerGeneration? _generation;
    private ulong _nextContext;
    private bool _invalid;

    public UuServerGeneration? Generation => _generation;

    public bool Observe(
        UuServerGeneration server,
        ulong log,
        ulong index,
        bool opened,
        bool successful,
        DateTimeOffset time)
    {
        if (_generation != server)
        {
            _generation = server;
            _contexts.Clear();
            _nextContext = 0;
            _invalid = false;
            _lifecycle.Begin(server, initiallyEmpty: server.StartedUtcTicks > traceStartedAt.UtcTicks);
        }

        if (_invalid) return false;
        if (time.UtcTicks < server.StartedUtcTicks)
        {
            Invalidate();
            return false;
        }

        var key = (log, index);
        ulong token;
        if (opened)
        {
            if (!successful) return true;
            if (!_contexts.TryGetValue(key, out token))
            {
                token = ++_nextContext;
                _contexts[key] = token;
            }
        }
        else if (!_contexts.Remove(key, out token))
        {
            token = ++_nextContext;
        }

        return _lifecycle.Observe(server, token, opened, time, successful);
    }

    public void Invalidate()
    {
        _invalid = true;
        _lifecycle.Invalidate();
    }

    public UuCaptureSnapshot Snapshot(DateTimeOffset now) => _lifecycle.Snapshot(now);
}
