using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Snug.Windows.Core;
using ListBox = System.Windows.Controls.ListBox;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Snug.Windows.Services;

/// <summary>Native touch panning plus UU's synthetic mouse drag. Retain
/// ordinary taps, wheel and keyboard input; a tap during inertia only stops it.</summary>
internal sealed class PhoneDragScroll
{
    private const double Slop = 8;
    private readonly ScrollViewer _viewer;
    private readonly DispatcherTimer _timer;
    private readonly MenuMomentum _momentum = new();
    private Point _start;
    private double _initialOffset, _coastOffset, _lastFrame;
    private bool _pending, _dragging, _stoppedByPress;
    private static double Now => Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;

    private PhoneDragScroll(ScrollViewer viewer)
    {
        _viewer=viewer;
        _timer=new DispatcherTimer(DispatcherPriority.Render,viewer.Dispatcher)
        {Interval=TimeSpan.FromSeconds(1d/60)};
        _timer.Tick+=Advance;
        viewer.PanningMode=PanningMode.VerticalOnly;
        viewer.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler(Down),true);
        viewer.AddHandler(UIElement.PreviewMouseMoveEvent,new System.Windows.Input.MouseEventHandler(Move),true);
        viewer.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler(Up),true);
        viewer.PreviewMouseWheel+=(_,_)=>Stop();
        viewer.PreviewKeyDown+=(_,_)=>Stop();
        viewer.PreviewTouchDown+=(_,_)=>Stop();
        viewer.ScrollChanged+=(_,e)=>{if(e.ExtentHeightChange!=0||e.ViewportHeightChange!=0)Stop();};
        viewer.LostMouseCapture+=LostCapture;
        viewer.IsVisibleChanged+=(_,_)=>{if(!viewer.IsVisible)Cancel();};
        viewer.Unloaded+=(_,_)=>Cancel();
    }

    public static void Attach(ScrollViewer viewer)
    {
        if(viewer.GetValue(Attached) is null)viewer.SetValue(Attached,new PhoneDragScroll(viewer));
    }
    public static void StopScrolling(ScrollViewer viewer)
    {
        if(viewer.GetValue(Attached) is PhoneDragScroll drag)drag.Cancel();
    }

    public static void AttachList(ListBox list)
    {
        list.Loaded+=(_,_)=>{list.ApplyTemplate();if(FindViewer(list) is { } viewer)Attach(viewer);};
    }

    private static readonly DependencyProperty Attached=DependencyProperty.RegisterAttached(
        "PhoneDragScrollAttached",typeof(PhoneDragScroll),typeof(PhoneDragScroll),new PropertyMetadata(null));
    private static ScrollViewer? FindViewer(DependencyObject node)
    {
        if(node is ScrollViewer viewer)return viewer;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
            if(FindViewer(VisualTreeHelper.GetChild(node,i)) is { } child)return child;
        return null;
    }

    private void Down(object sender,MouseButtonEventArgs e)
    {
        var wasCoasting=_momentum.IsCoasting;
        Cancel();
        if(e.StylusDevice is not null||_viewer.ScrollableHeight<=0)return;
        _pending=true;_start=e.GetPosition(_viewer);_initialOffset=_coastOffset=_viewer.VerticalOffset;
        _momentum.Begin(Now,_initialOffset,Math.Max(1,_viewer.ActualWidth/540));
        if(wasCoasting)
        {
            // Catch the moving list without opening the app below the finger.
            _stoppedByPress=true;
            Mouse.Capture(_viewer,CaptureMode.Element);
            e.Handled=true;
        }
    }

    private void Move(object sender,MouseEventArgs e)
    {
        if(!_pending||e.StylusDevice is not null)return;
        if(e.LeftButton!=MouseButtonState.Pressed){Cancel();return;}
        var p=e.GetPosition(_viewer);var dy=p.Y-_start.Y;var dx=p.X-_start.X;
        if(!_dragging)
        {
            if(Math.Abs(dy)<Slop||Math.Abs(dy)<Math.Abs(dx))return;
            _dragging=true;
            if(!Mouse.Capture(_viewer,CaptureMode.Element)){Cancel();return;}
        }
        MoveTo(dy);e.Handled=true;
    }

    private void MoveTo(double dy)
    {
        _coastOffset=Math.Clamp(_initialOffset-dy,0,_viewer.ScrollableHeight);
        _viewer.ScrollToVerticalOffset(_coastOffset);
        _momentum.Sample(Now,_coastOffset);
    }

    private void Up(object sender,MouseButtonEventArgs e)
    {
        if(_pending)
        {
            var p=e.GetPosition(_viewer);var dy=p.Y-_start.Y;
            // Some remote senders coalesce their final mouse move into release.
            if(!_dragging&&Math.Abs(dy)>=Slop&&Math.Abs(dy)>=Math.Abs(p.X-_start.X))_dragging=true;
            if(_dragging)
            {
                // Don't add a duplicate endpoint to stale samples: a pause
                // before release must not fling.
                var target=Math.Clamp(_initialOffset-dy,0,_viewer.ScrollableHeight);
                if(Math.Abs(target-_coastOffset)>.01)MoveTo(dy);
                e.Handled=true;
                StartCoast(Now);
            }
            else if(_stoppedByPress)e.Handled=true;
        }
        ResetPress();
    }
    private void StartCoast(double releaseTime)
    {
        if(!_momentum.Release(releaseTime))return;
        _lastFrame=Now;
        _timer.Start();
    }
    private void Advance(object? sender,EventArgs e)
    {
        if(!_viewer.IsVisible||!_momentum.IsCoasting){Stop();return;}
        var now=Now;var delta=_momentum.Advance(now-_lastFrame);_lastFrame=now;
        var next=Math.Clamp(_coastOffset+delta,0,_viewer.ScrollableHeight);
        _coastOffset=next;_viewer.ScrollToVerticalOffset(next);
        if(next<=0||next>=_viewer.ScrollableHeight||!_momentum.IsCoasting)Stop();
    }
    private void LostCapture(object sender,MouseEventArgs e)
    {
        if(_pending&&ReferenceEquals(e.OriginalSource,_viewer)&&!ReferenceEquals(Mouse.Captured,_viewer))Cancel();
    }
    private void Stop(){_timer.Stop();_momentum.Stop();}
    private void Cancel(){Stop();ResetPress();}
    private void ResetPress()
    {
        _pending=false;_dragging=false;_stoppedByPress=false;
        if(ReferenceEquals(Mouse.Captured,_viewer))Mouse.Capture(null);
    }
}