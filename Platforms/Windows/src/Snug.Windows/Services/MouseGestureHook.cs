using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Snug.Windows.Core;
using Snug.Windows.Interop;

namespace Snug.Windows.Services;

/// <summary>只接管 UU 注入且发生在手机屏区域内的左键操作。</summary>
public sealed class MouseGestureHook : IDisposable
{
    private static readonly UIntPtr InjectionMarker = new(0x534E5547);
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private readonly DispatcherTimer _coastTimer;
    private readonly TouchTranslator _translator = new();
    private IntPtr _hook;
    private WindowBounds? _region;
    private double _regionScale = 1;
    private PointD _heldPress;
    private bool _disposed;

    public MouseGestureHook()
    {
        _callback = OnMouse;
        _coastTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60)
        };
        _coastTimer.Tick += OnCoastTick;
    }

    public bool IsRunning => _hook != IntPtr.Zero;

    public void SetRegion(WindowBounds? region, double scale = 1)
    {
        _region = region;
        _regionScale = Math.Max(1, scale);
    }

    public bool Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MouseGestureHook));
        if (IsRunning) return true;
        var module = Process.GetCurrentProcess().MainModule?.ModuleName;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _callback,
            NativeMethods.GetModuleHandle(module),
            0);
        return IsRunning;
    }

    public void Stop()
    {
        _coastTimer.Stop();
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private void OnCoastTick(object? sender, EventArgs e)
    {
        if (_translator.MomentumFrame() is { } step) PostScroll(step);
        if (!_translator.IsCoasting) _coastTimer.Stop();
    }

    private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0 || _region is null) return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);

        var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
        if ((info.Flags & NativeMethods.LLMHF_INJECTED) == 0
            || info.ExtraInfo == InjectionMarker)
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);

        var message = wParam.ToInt32();
        var kind = message switch
        {
            NativeMethods.WM_LBUTTONDOWN => PointerKind.Press,
            NativeMethods.WM_MOUSEMOVE => PointerKind.Move,
            NativeMethods.WM_LBUTTONUP => PointerKind.Release,
            _ => (PointerKind?)null
        };
        if (kind is null) return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);

        var point = new PointD(info.Point.X, info.Point.Y);
        var region = _region.Value;
        var inside = point.X >= region.X && point.X < region.X + region.Width
            && point.Y >= region.Y && point.Y < region.Y + region.Height;
        var seconds = (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        var response = _translator.Handle(new TouchInput(kind.Value, point, seconds), inside, _regionScale);

        if (kind == PointerKind.Press && !response.PassOriginal) _heldPress = point;
        foreach (var command in response.Commands) Execute(command, kind.Value, point);

        if (kind == PointerKind.Release) _heldPress = default;
        if (_translator.IsCoasting && !_coastTimer.IsEnabled) _coastTimer.Start();

        return response.PassOriginal
            ? NativeMethods.CallNextHookEx(_hook, code, wParam, lParam)
            : new IntPtr(1);
    }

    private void Execute(TouchCommand command, PointerKind currentKind, PointD current)
    {
        switch (command.Kind)
        {
            case TouchCommandKind.ReplayPress:
                NativeMethods.SetCursorPos((int)_heldPress.X, (int)_heldPress.Y);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, InjectionMarker);
                break;

            case TouchCommandKind.ReplayCurrent:
                NativeMethods.SetCursorPos((int)current.X, (int)current.Y);
                if (currentKind == PointerKind.Release)
                    NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, InjectionMarker);
                break;

            case TouchCommandKind.RightClick:
                NativeMethods.SetCursorPos((int)command.Location.X, (int)command.Location.Y);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, InjectionMarker);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_RIGHTUP, 0, 0, 0, InjectionMarker);
                break;

            case TouchCommandKind.Scroll:
                if (command.Scroll is { } step) PostScroll(step);
                break;
        }
    }

    private static void PostScroll(ScrollStep step)
    {
        var delta = (short)Math.Clamp(step.PixelsY * 3, short.MinValue, short.MaxValue);
        if (delta == 0) return;
        var point = new NativeMethods.POINT { X = (int)step.Location.X, Y = (int)step.Location.Y };
        var target = NativeMethods.WindowFromPoint(point);
        if (target == IntPtr.Zero) return;

        var packedPoint = ((long)(point.Y & 0xFFFF) << 16) | (uint)(point.X & 0xFFFF);
        var packedDelta = (long)(ushort)delta << 16;
        NativeMethods.PostMessage(target, NativeMethods.WM_MOUSEWHEEL,
            new IntPtr(packedDelta), new IntPtr(packedPoint));
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _coastTimer.Tick -= OnCoastTick;
        _disposed = true;
    }
}
