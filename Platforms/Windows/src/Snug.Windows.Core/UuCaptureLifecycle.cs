namespace Snug.Windows.Core;

public enum UuCapturePhase { Unknown, Active, Rebuilding, Inactive }
public readonly record struct UuServerGeneration(int Pid, long StartedUtcTicks);
public readonly record struct UuCaptureSnapshot(UuCapturePhase Phase, int ObservedContexts, bool CompleteInventory)
{
    public bool? CaptureActive => Phase switch
    {
        UuCapturePhase.Active or UuCapturePhase.Rebuilding => true,
        UuCapturePhase.Inactive => false,
        _ => null
    };
}

/// <summary>State of the observed DxgKrnl output-duplication diagnostic
/// contexts. A verified 192/193 pair is capture evidence, not controller type
/// or monitor identity. Single-threaded; the observer owns source health.</summary>
public sealed class UuCaptureLifecycle
{
    public static readonly TimeSpan RestartGrace = TimeSpan.FromSeconds(3);
    private readonly HashSet<ulong> _active = [];
    private UuServerGeneration? _generation;
    private bool _healthy;
    private bool _complete;
    private DateTimeOffset? _lastStop, _lastEvent;

    // initiallyEmpty is trusted only if observation starts from a verified
    // no-capture baseline (e.g. newly observed server generation).
    public void Begin(UuServerGeneration server, bool initiallyEmpty)
    {
        _generation = server; _healthy = true; _complete = initiallyEmpty;
        _active.Clear(); _lastStop = null; _lastEvent = null;
    }

    public void Invalidate()
    {
        // Trace loss, observer failure or server replacement invalidates the
        // old inventory. Absence of events is never evidence of disconnect.
        _healthy = false; _complete = false;
        _active.Clear(); _lastStop = null; _lastEvent = null;
    }

    public bool Observe(UuServerGeneration server, ulong context, bool opened,
        DateTimeOffset time, bool successful = true)
    {
        if (!_healthy || _generation != server) return false;
        if (_lastEvent is { } last && time < last)
        {
            Invalidate(); return false;
        }
        _lastEvent = time;
        if (opened)
        {
            if (!successful) return false;
            _active.Add(context); _lastStop = null;
        }
        else
        {
            if (!_active.Remove(context))
            {
                // The trace started late or missed an open.
                _complete = false;
                return false;
            }
            if (_active.Count == 0) _lastStop = time;
        }
        return true;
    }

    public UuCaptureSnapshot Snapshot(DateTimeOffset now)
    {
        var phase = !_healthy ? UuCapturePhase.Unknown
            : _active.Count > 0 ? UuCapturePhase.Active
            : _lastStop is { } stop && now - stop < RestartGrace ? UuCapturePhase.Rebuilding
            : _complete ? UuCapturePhase.Inactive : UuCapturePhase.Unknown;
        return new(phase, _active.Count, _healthy && _complete);
    }

    // A proven-inactive capture inventory supersedes sticky log handles.
    // Independent overlay, peer and controller-table evidence keeps its precedence.
    // An incomplete inventory cannot suppress the existing fallback.
    public static UuSignals Combine(UuSignals current, UuCaptureSnapshot capture) =>
        capture.CaptureActive switch
        {
            true => current with { CaptureActive = true, CaptureVerifiedInactive = false,
                CaptureVerifiedActive = capture.Phase == UuCapturePhase.Active &&
                    capture.CompleteInventory && capture.ObservedContexts > 0 },
            false when capture.CompleteInventory && capture.ObservedContexts == 0
                => current with { CaptureActive = false, LogFallback = false, CaptureVerifiedInactive = true, CaptureVerifiedActive = false },
            _ => current with { CaptureVerifiedInactive = false, CaptureVerifiedActive = false }
        };
}