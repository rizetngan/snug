using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Snug.Windows.Core;

namespace Snug.Windows;

public sealed class AppIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is AppInfo app ? AppIconCache.Get(app) : null;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}

public sealed class FirstLetterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text)) return "A";
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text.Trim());
        return enumerator.MoveNext() ? enumerator.GetTextElement() : "A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}

internal static class AppIconCache
{
    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiLargeIcon = 0x00000000;
    private static readonly ConcurrentDictionary<string, Lazy<ImageSource?>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Get(AppInfo app) =>
        Cache.GetOrAdd(app.AppId, _ => new Lazy<ImageSource?>(() => Load(app))).Value;

    private static ImageSource? Load(AppInfo app)
    {
        var candidates = new[] { app.IconPath, app.ExecutablePath, app.LaunchTarget }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates)
        {
            IntPtr icon = IntPtr.Zero;
            try
            {
                var result = SHGetFileInfo(path!, 0, out var info,
                    (uint)Marshal.SizeOf<SHFILEINFO>(), ShgfiIcon | ShgfiLargeIcon);
                icon = info.Icon;
                if (result == IntPtr.Zero || icon == IntPtr.Zero) continue;

                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(96, 96));
                source.Freeze();
                return source;
            }
            catch (DllNotFoundException) { return null; }
            catch (EntryPointNotFoundException) { return null; }
            catch (ArgumentException) { }
            finally
            {
                if (icon != IntPtr.Zero) DestroyIcon(icon);
            }
        }
        return null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path, uint fileAttributes, out SHFILEINFO info, uint fileInfoSize, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);
}
