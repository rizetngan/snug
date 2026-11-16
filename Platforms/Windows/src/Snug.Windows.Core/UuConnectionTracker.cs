namespace Snug.Windows.Core;

public enum ControllerKind { Unknown, Phone, Desktop }

public sealed record ControllerInfo(string Name, ControllerKind Kind);

public readonly record struct UuSignals(
    bool OverlayVisible,
    ControllerInfo? Controller,
    bool? CaptureActive)
{
    public bool IsPresent => OverlayVisible || Controller is not null || CaptureActive == true;
}

public enum ConnectionTransitionKind { Connected, Resumed, ControllerChanged, Interrupted, Ended }

public sealed record ConnectionTransition(ConnectionTransitionKind Kind, ControllerInfo? Controller);

public sealed class UuConnectionTracker
{
    public static readonly TimeSpan DisconnectDebounce = TimeSpan.FromSeconds(1.5);
    public static readonly TimeSpan QuietWindow = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ReconnectWindow = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan UnknownControllerGrace = TimeSpan.FromSeconds(2);

    private DateTimeOffset? _lastPresentAt;
    private DateTimeOffset? _sessionStartedAt;
    private DateTimeOffset? _quietUntil;
    private DateTimeOffset? _interruptedAt;
    private ControllerInfo? _controller;
    private bool _connected;

    public bool IsConnected => _connected;
    public ControllerInfo? Controller => _controller;
    public bool WantsPhoneScreen(DateTimeOffset now)
    {
        var inSession = _connected || (_interruptedAt is { } interrupted && now - interrupted <= ReconnectWindow);
        if (!inSession) return false;
        if (_controller?.Kind == ControllerKind.Desktop) return false;
        if (_controller?.Kind == ControllerKind.Phone) return true;
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
        if (signals.IsPresent)
        {
            _lastPresentAt = now;
            if (!_connected)
            {
                var resumed = _interruptedAt is { } interrupted && now - interrupted <= ReconnectWindow;
                _sessionStartedAt ??= now;
                _connected = true;
                if (signals.Controller is not null) _controller = signals.Controller;
                _interruptedAt = null;
                transitions.Add(new(resumed ? ConnectionTransitionKind.Resumed : ConnectionTransitionKind.Connected, _controller));
            }
            else if (signals.Controller is { } next && _controller != next)
            {
                _controller = next;
                transitions.Add(new(ConnectionTransitionKind.ControllerChanged, _controller));
            }
            return transitions;
        }

        if (_connected && _lastPresentAt is { } last
            && now - last > DisconnectDebounce
            && (_quietUntil is null || now > _quietUntil))
        {
            _connected = false;
            _interruptedAt = now;
            transitions.Add(new(ConnectionTransitionKind.Interrupted, _controller));
        }
        else if (!_connected && _interruptedAt is { } at && now - at > ReconnectWindow)
        {
            _interruptedAt = null;
            _sessionStartedAt = null;
            _controller = null;
            transitions.Add(new(ConnectionTransitionKind.Ended, null));
        }

        return transitions;
    }
}
