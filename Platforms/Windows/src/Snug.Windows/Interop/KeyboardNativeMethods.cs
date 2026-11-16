using System.Runtime.InteropServices;

namespace Snug.Windows.Interop;

internal static class KeyboardNativeMethods
{
    [DllImport("user32.dll")]
    internal static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
