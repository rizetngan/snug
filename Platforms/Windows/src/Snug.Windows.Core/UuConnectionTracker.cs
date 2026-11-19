namespace Snug.Windows.Core;

public enum ControllerKind { Unknown, Phone, Desktop }
public sealed record ControllerInfo(string Name, ControllerKind Kind);

public readonly record struct UuSignals(
    bool OverlayVisible,
    ControllerInfo? Controller,
    bool? CaptureActive,
    IReadOnlyList<ControllerInfo>? Controllers = null,
    bool LogFallback = false,
    bool PeerConnection = false,
    bool CaptureVerifiedInactive = false)
{
    public bool IsPresent => OverlayVisible || Controller is not null ||
        Controllers is { Count: > 0 } || CaptureActive == true || LogFallback;

    // Peer TCP is diagnostic context only: UU can keep a server-owned peer alive after capture has ended.
    // Session presence follows the same three observable signals as upstream: overlay, controller table, or capture.
    // LogFallback remains compatibility evidence, not verified identity.
    public UuEvidenceKind Evidence =>
        Controllers is { Count: > 0 } || Controller is { } item &&
            !(item.Kind == ControllerKind.Unknown && item.Name == "UU Remote")
            ? UuEvidenceKind.ControllerTable
            : OverlayVisible || CaptureActive == true ? UuEvidenceKind.Session
            : LogFallback || Controller is not null ? UuEvidenceKind.LogFallback
            : UuEvidenceKind.None;
}

public enum UuEvidenceKind { None, LogFallback, Session, ControllerTable }

public enum ConnectionTransitionKind { Connected, Resumed, ControllerChanged, Interrupted, Ended }
public sealed record ConnectionTransition(ConnectionTransitionKind Kind, ControllerInfo? Controller);

/// <summary>Windows connection/episode state; follows upstream RemoteSession and ControllerKind.</summary>
public sealed class UuConnectionTracker
{
    public static readonly TimeSpan DisconnectDebounce = TimeSpan.FromSeconds(1.5);
    public static readonly TimeSpan QuietWindow = TimeSpan.FromSeconds(5);
    // Full session interruption: 6s trial. Keep the accepted longer grace
    // for a departed controller while another controller is still present.
    public static readonly TimeSpan ReconnectWindow = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan VerifiedInactiveGrace = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan DepartedControllerGrace = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan UnknownControllerGrace = TimeSpan.FromSeconds(2);

    private DateTimeOffset? _lastPresentAt;
    private DateTimeOffset? _sessionStartedAt;
    private DateTimeOffset? _quietUntil;
    private DateTimeOffset? _interruptedAt;
    private DateTimeOffset? _verifiedInactiveSince;
    private List<ControllerInfo> _controllers = [];
    private readonly Dictionary<string, (ControllerInfo Controller, DateTimeOffset Until)> _departed =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _connected;

    public bool IsConnected => _connected;
    public IReadOnlyList<ControllerInfo> Controllers => _controllers.ToArray();
    public ControllerInfo? Controller =>
        _controllers.FirstOrDefault(c => c.Kind == ControllerKind.Phone)
        ?? _controllers.FirstOrDefault(c => c.Kind == ControllerKind.Unknown)
        ?? _controllers.FirstOrDefault();

    public bool WantsPhoneScreen(DateTimeOffset now)
    {
        var inSession = _connected || (_interruptedAt is { } at && now - at <= ReconnectWindow);
        if (!inSession) return false;
        var active = _controllers.Concat(_departed.Values
            .Where(item => now <= item.Until)
            .Select(item => item.Controller)).ToArray();
        if (active.Any(c => c.Kind == ControllerKind.Phone)) return true;
        if (active.Length > 0 && active.All(c => c.Kind == ControllerKind.Desktop)) return false;
        return _sessionStartedAt is { } started && now - started >= UnknownControllerGrace;
    }

    public void NoteSelfInducedChange(DateTimeOffset now)
    {
        var until = now + QuietWindow;
        if (_quietUntil is null || until > _quietUntil) _quietUntil = until;
    }

    public IReadOnlyList<ConnectionTransition> Update(UuSignals signals, DateTimeOffset now)
    {
        var transitions = new List<ConnectionTransition>();
        // A fresh, complete and continuously inactive capture inventory can
        // shorten an already-interrupted episode, never a positive signal.
        // Loss of inventory or positive evidence resets the early deadline.
        if (signals.CaptureVerifiedInactive && signals.CaptureActive == false && !signals.IsPresent)
            _verifiedInactiveSince ??= now;
        else
            _verifiedInactiveSince = null;
        // Keep the full reconnect grace for unknown/incomplete capture.
        // If a positive sample arrives just before evaluation of the early
        // deadline, it wins; normal expiry still follows upstream ordering.
        if (!_connected && _interruptedAt is { } old &&
            (now - old > ReconnectWindow ||
             (_verifiedInactiveSince is { } verified &&
              now - old > VerifiedInactiveGrace &&
              now - verified > VerifiedInactiveGrace &&
              (_quietUntil is null || now > _quietUntil))))
        {
            _interruptedAt = null;
            _verifiedInactiveSince = null;
            _sessionStartedAt = null;
            _lastPresentAt = null;
            _quietUntil = null;
            _controllers.Clear();
            _departed.Clear();
            transitions.Add(new(ConnectionTransitionKind.Ended, null));
        }

        foreach (var name in _departed.Where(item => now > item.Value.Until)
                     .Select(item => item.Key).ToArray())
            _departed.Remove(name);

        if (signals.IsPresent)
        {
            _lastPresentAt = now;
            // An unreadable/empty controller table is not negative evidence.
            // "UU Remote" is the Windows log-only placeholder, not a device.
            var next = GetControllers(signals);
            if (!_connected)
            {
                var resumed = _interruptedAt is not null;
                _sessionStartedAt ??= now;
                _connected = true;
                if (next.Count > 0) Regroup(next, now);
                _interruptedAt = null;
                transitions.Add(new(resumed ? ConnectionTransitionKind.Resumed : ConnectionTransitionKind.Connected, Controller));
            }
            else if (next.Count > 0 && !_controllers.ToHashSet().SetEquals(next))
            {
                Regroup(next, now);
                transitions.Add(new(ConnectionTransitionKind.ControllerChanged, Controller));
            }
            return transitions;
        }

        if (_connected && _lastPresentAt is { } last
            && now - last > DisconnectDebounce
            && (_quietUntil is null || now > _quietUntil))
        {
            _connected = false;
            _interruptedAt = now;
            transitions.Add(new(ConnectionTransitionKind.Interrupted, Controller));
        }
        return transitions;
    }

    private static List<ControllerInfo> GetControllers(UuSignals signals)
    {
        var source = signals.Controllers ?? (signals.Controller is { } item ? [item] : []);
        return source.Where(c => !string.IsNullOrWhiteSpace(c.Name)
                && !(c.Kind == ControllerKind.Unknown && c.Name == "UU Remote"))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last()).ToList();
    }

    private void Regroup(List<ControllerInfo> next, DateTimeOffset now)
    {
        var names = next.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var previous in _controllers)
            if (!names.Contains(previous.Name))
                _departed[previous.Name] = (previous, now + DepartedControllerGrace);
        foreach (var controller in next) _departed.Remove(controller.Name);
        _controllers = next;
    }
}