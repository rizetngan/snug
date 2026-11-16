using System.Windows;
using System.Windows.Media.Animation;
using Snug.Windows.Core;
using Snug.Windows.Interop;
using Snug.Windows.Services;

namespace Snug.Windows;

public partial class HomeBarWindow : Window
{
    private readonly AppController _controller;
    private DisplayInfo? _targetDisplay;

    public HomeBarWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
        Opacity = 0;
        Loaded += (_, _) => ApplyPlacement();
    }

    public void PlaceOn(DisplayInfo display)
    {
        _targetDisplay = display;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0;
        Top = 0;
        Width = display.Bounds.Width / display.DpiScaleX;
        Height = ScreenFit.HomeBarHeight(display.Bounds.Width / 2.0) * 2 / display.DpiScaleY;
        if (IsLoaded) ApplyPlacement();
    }

    private void ApplyPlacement()
    {
        if (_targetDisplay is { } display)
        {
            var barHeight = ScreenFit.HomeBarHeight(display.Bounds.Width / 2.0) * 2;
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var placed = NativeMethods.SetWindowPos(handle, IntPtr.Zero,
                display.Bounds.X,
                display.Bounds.Y + display.Bounds.Height - barHeight,
                display.Bounds.Width,
                barHeight,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            if (!placed)
                Top = (display.Bounds.Y + display.Bounds.Height - barHeight) / display.DpiScaleY;
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE,
            style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
    }

    private void ShowList_Click(object sender, RoutedEventArgs e) => _controller.ShowPhoneList();
}
