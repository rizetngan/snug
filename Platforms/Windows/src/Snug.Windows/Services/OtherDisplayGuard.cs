using System.Runtime.InteropServices;
using Snug.Windows.Core;
using Snug.Windows.Interop;

namespace Snug.Windows.Services;

/// <summary>Preserve non-Snug monitors across a Snug-owned topology or mode operation.
/// No registry writes; no blanket display reset. Only a uniquely identified monitor
/// whose live mode changed during this operation can be restored.</summary>
internal static class OtherDisplayGuard
{
    private const int CurrentSettings = -1;
    private const uint TestOnly = 0x00000002;
    private const uint Fields = 0x00000020 | 0x00000080 | 0x00080000 |
        0x00100000 | 0x00400000 | 0x00040000 | 0x00200000 | 0x20000000;

    public static IReadOnlyList<ProtectedDisplayMode> Capture()
    {
        var result = new List<ProtectedDisplayMode>();
        foreach (var display in new DisplayEnumerator().GetDisplays())
        {
            if (PhoneDisplayModeSwitcher.IsSnugDisplay(display) ||
                string.IsNullOrWhiteSpace(display.DeviceId)) continue;
            var mode = NewMode();
            if (!NativeMethods.EnumDisplaySettings(display.DeviceName, CurrentSettings, ref mode)) continue;
            result.Add(new(display.DeviceId, display.DeviceName, display.IsPrimary,
                mode.PositionX, mode.PositionY, mode.PelsWidth, mode.PelsHeight,
                mode.DisplayFrequency, mode.BitsPerPel, mode.DisplayOrientation,
                mode.DisplayFixedOutput, mode.DisplayFlags));
        }
        return result;
    }

    public static async Task<bool> PreserveAsync(IReadOnlyList<ProtectedDisplayMode> before)
    {
        if (before.Count == 0) return true;
        // The driver acknowledgement can precede the actual topology change.
        await Task.Delay(350);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var current = Capture();
            var changes = DisplayModeProtection.FindDrift(before, current);
            if (changes.Count == 0)
            {
                if (attempt > 0) return true;
                await Task.Delay(350);
                continue;
            }
            foreach (var change in changes)
            {
                // Repeat identity checks immediately before invoking the OS.
                var live = Capture().Where(d =>
                    StringComparer.OrdinalIgnoreCase.Equals(d.DeviceId, change.Before.DeviceId)).ToArray();
                if (live.Length != 1 || live[0].IsPrimary != change.Before.IsPrimary) continue;
                if (change.Before.SameMode(live[0])) continue;
                if (!change.Current.SameMode(live[0])) continue;
                var mode = NewMode();
                if (!NativeMethods.EnumDisplaySettings(live[0].DeviceName, CurrentSettings, ref mode)) continue;
                var old = change.Before;
                mode.PositionX = old.X;
                mode.PositionY = old.Y;
                mode.PelsWidth = old.Width;
                mode.PelsHeight = old.Height;
                mode.DisplayFrequency = old.Hertz;
                mode.BitsPerPel = old.BitsPerPixel;
                mode.DisplayOrientation = old.Orientation;
                mode.DisplayFixedOutput = old.FixedOutput;
                mode.DisplayFlags = old.Flags;
                mode.Fields = Fields;
                if (NativeMethods.ChangeDisplaySettingsEx(live[0].DeviceName, ref mode,
                    IntPtr.Zero, TestOnly, IntPtr.Zero) != 0) continue;
                NativeMethods.ChangeDisplaySettingsEx(live[0].DeviceName, ref mode,
                    IntPtr.Zero, 0, IntPtr.Zero);
            }
            await Task.Delay(350);
        }
        return DisplayModeProtection.FindDrift(before, Capture()).Count == 0;
    }

    private static NativeMethods.DEVMODE NewMode() => new()
    {
        Size = checked((ushort)Marshal.SizeOf<NativeMethods.DEVMODE>()),
        DeviceName = string.Empty,
        FormName = string.Empty
    };
}