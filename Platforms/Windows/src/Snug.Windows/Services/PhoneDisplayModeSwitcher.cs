using System.Runtime.InteropServices;
using Snug.Windows.Core;
using Snug.Windows.Interop;

namespace Snug.Windows.Services;

/// <summary>
/// Changes only the Snug-owned monitor's transient Windows display mode.
/// Other displays and the user's persisted desktop configuration are untouched.
/// </summary>
public sealed class PhoneDisplayModeSwitcher
{
    private const int CurrentSettings = -1;
    private const int Successful = 0;
    private const uint TestOnly = 0x00000002;
    private const uint Fields = 0x00080000 | 0x00100000 | 0x00400000;

    /// <summary>
    /// Match the raw driver mode, not Screen.AllScreens bounds. With Windows
    /// display scaling the 1080x1834 VDD can appear as a 720x1223 desktop.
    /// Upstream Snug likewise separates physical modes from logical UI sizes.
    /// </summary>
    public bool HasMode(DisplayInfo display, VddResolutionMode target)
    {
        if (!IsSnugDisplay(display)) return false;
        var current = CreateMode();
        return NativeMethods.EnumDisplaySettings(display.DeviceName, CurrentSettings, ref current)
            && current.PelsWidth == target.Width
            && current.PelsHeight == target.Height
            && current.DisplayFrequency == target.RefreshRate;
    }

    public bool TryChange(DisplayInfo display, VddResolutionMode target, out string error)
    {
        error = string.Empty;
        if (!IsSnugDisplay(display))
        {
            error = "目标不是 Snug 专用手机屏，未更改任何显示器。";
            return false;
        }

        if (target.Width < 800 || target.Height < 1000 || target.RefreshRate != 60)
        {
            error = "手机屏分辨率超出已验证的驱动范围。";
            return false;
        }

        if (Marshal.SizeOf<NativeMethods.DEVMODE>() != 220)
        {
            error = "Windows 显示模式结构校验未通过，已取消切换。";
            return false;
        }

        var current = CreateMode();
        if (!NativeMethods.EnumDisplaySettings(display.DeviceName, CurrentSettings, ref current))
        {
            error = "无法读取 Snug 显示器当前模式。";
            return false;
        }

        if (current.PelsWidth == target.Width
            && current.PelsHeight == target.Height
            && current.DisplayFrequency == target.RefreshRate)
            return true;

        // Reject modes not explicitly offered by Windows for this exact monitor.
        var found = false;
        var selected = CreateMode();
        for (var index = 0; index < 512; index++)
        {
            var mode = CreateMode();
            if (!NativeMethods.EnumDisplaySettings(display.DeviceName, index, ref mode)) break;
            if (mode.PelsWidth != target.Width || mode.PelsHeight != target.Height
                || mode.DisplayFrequency != target.RefreshRate) continue;
            selected = mode;
            found = true;
            break;
        }
        if (!found)
        {
            error = $"Windows 未向 Snug 显示器公布 {target.Width} × {target.Height} @60Hz 模式。";
            return false;
        }

        selected.Fields = Fields;
        var check = NativeMethods.ChangeDisplaySettingsEx(display.DeviceName, ref selected,
            IntPtr.Zero, TestOnly, IntPtr.Zero);
        if (check != Successful)
        {
            error = $"Snug 显示模式预检失败：{check}。";
            return false;
        }

        var result = NativeMethods.ChangeDisplaySettingsEx(display.DeviceName, ref selected,
            IntPtr.Zero, 0, IntPtr.Zero);
        if (result != Successful)
        {
            error = $"Snug 显示模式切换失败：{result}。";
            return false;
        }

        return true;
    }

    public static bool IsSnugDisplay(DisplayInfo display) =>
        !display.IsPrimary
        && (display.DeviceId.Contains("SNG00F0", StringComparison.OrdinalIgnoreCase)
            || display.FriendlyName.Contains("Snug Phone", StringComparison.OrdinalIgnoreCase));

    private static NativeMethods.DEVMODE CreateMode() => new()
    {
        Size = checked((ushort)Marshal.SizeOf<NativeMethods.DEVMODE>()),
        DeviceName = string.Empty,
        FormName = string.Empty
    };
}
