namespace Snug.Windows.Core;

/// <summary>Identity and bounds captured when pressing a real native close button.</summary>
public readonly record struct CloseButtonHit(WindowKey Key, WindowBounds Frame, PointD Point);

public sealed class CloseTapGuard
{
    private CloseButtonHit? _pressed;
    private double _pressedAt;
    private double _slop;
    private bool _dragged;

    public bool Tracking => _pressed.HasValue;

    public void Begin(CloseButtonHit hit, double seconds, double scale)
    {
        _pressed = hit;
        _pressedAt = seconds;
        _slop = TouchTranslator.Slop * Math.Max(1, scale);
        _dragged = false;
    }

    public void Move(PointD point)
    {
        if (_pressed is not { } hit) return;
        double dx = point.X - hit.Point.X, dy = point.Y - hit.Point.Y;
        if (dx * dx + dy * dy >= _slop * _slop) _dragged = true;
    }

    public CloseButtonHit? Release(CloseButtonHit? current, PointD point, double seconds)
    {
        Move(point);
        var original = _pressed;
        var valid = !_dragged && seconds - _pressedAt < TouchTranslator.HoldDuration;
        Reset();
        if (!valid || original is not { } pressed || current is not { } released ||
            pressed.Key != released.Key || pressed.Frame != released.Frame)
            return null;
        return released;
    }

    public void Reset() { _pressed = null; _dragged = false; }
}