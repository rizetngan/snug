namespace Snug.Windows.Core;

/// <summary>
/// Native browser tab strips and address controls require unmodified mouse
/// down/up. UU's touch-to-scroll conversion applies to the page below.
/// </summary>
public static class BrowserChromeHitTest
{
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "brave", "firefox", "vivaldi", "opera", "arc"
    };

    public static bool ShouldPassThrough(string processName, WindowBounds bounds, PointD point, double scale)
    {
        if (!BrowserProcesses.Contains(processName) || bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        var headerHeight = Math.Min(bounds.Height, Math.Clamp(86 * Math.Max(1, scale), 86, 148));
        return point.X >= bounds.X && point.X < bounds.X + bounds.Width
            && point.Y >= bounds.Y && point.Y < bounds.Y + headerHeight;
    }
}