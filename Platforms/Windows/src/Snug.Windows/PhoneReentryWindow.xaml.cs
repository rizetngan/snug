using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Snug.Windows.Core;
using Snug.Windows.Interop;

namespace Snug.Windows;

public partial class PhoneReentryWindow : Window
{
    private readonly Func<Task> _enter;
    private DisplayInfo? _display;
    private System.Drawing.Rectangle _work;
    private bool _busy;

    public PhoneReentryWindow(Func<Task> enter)
    {
        _enter = enter;
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000;
        Top = -10000;
        Opacity = 0;
        Loaded += (_, _) => ApplyPlacement();
    }

    public void Present(DisplayInfo display)
    {
        var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(
            s => string.Equals(s.DeviceName, display.DeviceName, StringComparison.OrdinalIgnoreCase));
        if (screen is null) { Hide(); return; }
        var work = screen.WorkingArea;
        var changed = _display != display || _work != work;
        _display = display;
        _work = work;
        if (IsVisible)
        {
            if (changed) ApplyPlacement();
            return;
        }
        Show();
        ApplyPlacement();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
    }

    private void ApplyPlacement()
    {
        if (_display is not { } display) return;
        int width = (int)Math.Round(Width * display.DpiScaleX);
        int height = (int)Math.Round(Height * display.DpiScaleY);
        var marginX = (int)Math.Round(16 * display.DpiScaleX);
        var marginY = (int)Math.Round(16 * display.DpiScaleY);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(handle, new IntPtr(-1),
            Math.Max(_work.Left, _work.Right - width - marginX),
            Math.Max(_work.Top, _work.Bottom - height - marginY),
            width, height, NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE,
            style | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private async void Enter_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        EnterButton.IsEnabled = false;
        try { await _enter(); }
        finally { _busy = false; EnterButton.IsEnabled = true; }
    }
}