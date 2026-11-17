using System.Runtime.InteropServices;

namespace Snug.Windows.Interop;

/// <summary>WH_MOUSE_LL points are physical pixels even when a hosted WPF UI
/// uses DPI-virtualized coordinates. Scope only the native input operations.</summary>
internal readonly struct PhysicalDpiScope : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);

    private readonly IntPtr _previous;
    public PhysicalDpiScope() => _previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
    public bool IsValid => _previous != IntPtr.Zero;
    public void Dispose()
    {
        if (IsValid) SetThreadDpiAwarenessContext(_previous);
    }
}