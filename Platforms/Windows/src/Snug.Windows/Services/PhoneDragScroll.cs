using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ListBox = System.Windows.Controls.ListBox;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Snug.Windows.Services;

/// <summary>Touch panning for actual touch devices, and direct mouse-drag
/// scrolling for UU's screen-touch mode (which injects mouse down/move/up).
/// Native wheel, keyboard, and button taps are left unchanged.</summary>
internal sealed class PhoneDragScroll
{
    private const double Slop = 8;
    private readonly ScrollViewer _viewer;
    private Point _start;
    private double _initialOffset;
    private bool _pending;
    private bool _dragging;

    private PhoneDragScroll(ScrollViewer viewer)
    {
        _viewer = viewer;
        viewer.PanningMode = PanningMode.VerticalOnly;
        viewer.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(Down), true);
        viewer.AddHandler(UIElement.PreviewMouseMoveEvent, new System.Windows.Input.MouseEventHandler(Move), true);
        viewer.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,
            new MouseButtonEventHandler(Up), true);
        viewer.LostMouseCapture += LostCapture;
        viewer.IsVisibleChanged += (_, _) => { if (!viewer.IsVisible) Reset(); };
        viewer.Unloaded += (_, _) => Reset();
    }

    public static void Attach(ScrollViewer viewer) => _ = new PhoneDragScroll(viewer);

    public static void AttachList(ListBox list)
    {
        list.Loaded += (_, _) =>
        {
            list.ApplyTemplate();
            if (FindViewer(list) is { } viewer && !(bool)viewer.GetValue(Attached))
            {
                viewer.SetValue(Attached, true);
                Attach(viewer);
            }
        };
    }

    private static readonly DependencyProperty Attached = DependencyProperty.RegisterAttached(
        "PhoneDragScrollAttached", typeof(bool), typeof(PhoneDragScroll), new PropertyMetadata(false));

    private static ScrollViewer? FindViewer(DependencyObject node)
    {
        if (node is ScrollViewer viewer) return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindViewer(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }

    private void Down(object sender, MouseButtonEventArgs e)
    {
        Reset();
        // Real touch events use WPF's native panning. Synthetic UU mouse
        // events have no StylusDevice and need this separate adapter.
        if (e.StylusDevice is not null || _viewer.ScrollableHeight <= 0) return;
        _pending = true;
        _start = e.GetPosition(_viewer);
        _initialOffset = _viewer.VerticalOffset;
        // Do not swallow the press: short taps still act on the actual button.
    }

    private void Move(object sender, MouseEventArgs e)
    {
        if (!_pending || e.StylusDevice is not null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { Reset(); return; }
        var point = e.GetPosition(_viewer);
        var dy = point.Y - _start.Y;
        var dx = point.X - _start.X;
        if (!_dragging)
        {
            if (Math.Abs(dy) < Slop || Math.Abs(dy) < Math.Abs(dx)) return;
            // Taking capture cancels the pressed child button's click.
            // Capture only after a real drag so taps retain native semantics.
            _dragging = true;
            if (!Mouse.Capture(_viewer, CaptureMode.Element))
            {
                Reset();
                return;
            }
        }
        _viewer.ScrollToVerticalOffset(Math.Clamp(_initialOffset - dy, 0, _viewer.ScrollableHeight));
        e.Handled = true;
    }

    private void Up(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            var dy = e.GetPosition(_viewer).Y - _start.Y;
            _viewer.ScrollToVerticalOffset(Math.Clamp(_initialOffset - dy, 0, _viewer.ScrollableHeight));
            e.Handled = true;
        }
        Reset();
    }

    private void LostCapture(object sender, MouseEventArgs e)
    {
        // Child buttons lose capture when scrolling begins. Only losing
        // the viewer's own capture cancels the active scroll.
        if (_dragging && ReferenceEquals(e.OriginalSource, _viewer) &&
            !ReferenceEquals(Mouse.Captured, _viewer))
            Reset();
    }

    private void Reset()
    {
        _pending = false;
        _dragging = false;
        if (ReferenceEquals(Mouse.Captured, _viewer)) Mouse.Capture(null);
    }
}