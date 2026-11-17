using System.Runtime.InteropServices;
using Snug.Windows.Core;
using Snug.Windows.Interop;

namespace Snug.Windows.Services;

/// <summary>Never infer a window-close button from a browser/titlebar rectangle.
/// Require the actual Win32 non-client hit, frontmost HWND, PID and unchanged frame.</summary>
internal static class NativeCloseHitTest
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg,
        IntPtr wp, IntPtr lp, uint flags, uint timeout, out UIntPtr result);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    public static CloseButtonHit? Capture(WindowKey key, PointD point)
    {
        using var dpi = new PhysicalDpiScope();
        if (!dpi.IsValid) return null;
        var hwnd = new IntPtr(key.Handle);
        if (NativeMethods.GetForegroundWindow() != hwnd ||
            !NativeMethods.IsWindow(hwnd) || NativeMethods.IsHungAppWindow(hwnd) ||
            !NativeMethods.GetWindowRect(hwnd, out var rect)) return null;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != key.ProcessId) return null;
        var x = (int)point.X;
        var y = (int)point.Y;
        // Native caption controls live in the top strip. Keep normal content
        // gestures fast and do not send synchronous messages to every tap.
        var maximumCaption = 100 * Math.Max(1.0, GetDpiForWindow(hwnd) / 96.0);
        if (x < rect.Left || x >= rect.Right || y < rect.Top ||
            y >= Math.Min(rect.Bottom, rect.Top + maximumCaption)) return null;
        if (x < short.MinValue || x > short.MaxValue || y < short.MinValue || y > short.MaxValue)
            return null;
        var target = NativeMethods.WindowFromPoint(new NativeMethods.POINT { X = x, Y = y });
        if (target == IntPtr.Zero || NativeMethods.GetAncestor(target, 2) != hwnd) return null;
        var packed = new IntPtr(unchecked((int)((uint)(ushort)x | ((uint)(ushort)y << 16))));
        // WM_NCHITTEST; bounded wait. Only HTCLOSE (20) is accepted.
        if (SendMessageTimeout(hwnd, 0x0084, IntPtr.Zero, packed, 0x0002, 25, out var result) == IntPtr.Zero ||
            result.ToUInt64() != 20) return null;
        return new(key, new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), point);
    }

    public static bool Revalidate(CloseButtonHit hit) => Capture(hit.Key, hit.Point) is { } current &&
        current.Key == hit.Key && current.Frame == hit.Frame;
}