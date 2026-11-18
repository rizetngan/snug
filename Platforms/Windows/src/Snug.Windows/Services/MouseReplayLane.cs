using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Snug.Windows.Core;
using Snug.Windows.Interop;

namespace Snug.Windows.Services;

internal enum MouseReplayKind { Move, LeftDown, LeftUp, RightDown, RightUp }
internal readonly record struct MouseReplayAction(MouseReplayKind Kind, PointD Point);

/// <summary>
/// Sends batches of replayed gestures from a different thread than WH_MOUSE_LL.
/// The hook must never synchronously generate input: it can wait on its own
/// hook chain until LowLevelHooksTimeout (1000ms on current Windows).
/// </summary>
internal sealed class MouseReplayLane : IDisposable
{
    private sealed record Batch(int Generation, MouseReplayAction[] Actions);
    private readonly BlockingCollection<Batch> _queue = new();
    private readonly Thread _thread;
    private readonly UIntPtr _marker;
    private int _generation;
    private bool _leftDown, _rightDown; // replay worker only

    internal MouseReplayLane(UIntPtr marker)
    {
        _marker = marker;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "Snug.Windows.MouseReplay",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    // Producer does not wait for injection or the target application's UI.
    internal void Enqueue(MouseReplayAction[] actions) =>
        _queue.Add(new(Volatile.Read(ref _generation), actions));

    internal void Invalidate()
    {
        var next = Interlocked.Increment(ref _generation);
        _queue.Add(new(next, [])); // wake the worker to release any held button
    }

    private void Run()
    {
        using var dpi = new PhysicalDpiScope();
        if (!dpi.IsValid) return;
        var active = -1;
        try
        {
            foreach (var batch in _queue.GetConsumingEnumerable())
            {
                var generation = Volatile.Read(ref _generation);
                if (active != generation)
                {
                    ReleaseHeld();
                    active = generation;
                }
                if (batch.Generation != generation || batch.Actions.Length == 0) continue;
                SendBatch(batch.Actions, generation);
            }
        }
        finally { ReleaseHeld(); }
    }

    private const uint Move = 0x0001, LeftDown = 0x0002, LeftUp = 0x0004,
        RightDown = 0x0008, RightUp = 0x0010, Absolute = 0x8000, VirtualDesk = 0x4000;
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public UIntPtr ExtraInfo;
    }
    // MouseInput is the largest member of INPUT's union on x86 and x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private Input Event(uint flags, int x = 0, int y = 0) =>
        new() { Type = 0, Mouse = new() { X = x, Y = y, Flags = flags, ExtraInfo = _marker } };

    private static int Normalize(double point, int origin, int extent) =>
        (int)Math.Clamp(Math.Floor(((point - origin) * 65536.0 + 32768.0) / extent), 0, 65535);

    private void SendBatch(MouseReplayAction[] actions, int generation)
    {
        // Physical desktop coordinates; keep multi-monitor and high-DPI
        // behavior intact, including monitors with a negative origin.
        var left = GetSystemMetrics(76);
        var top = GetSystemMetrics(77);
        var width = GetSystemMetrics(78);
        var height = GetSystemMetrics(79);
        if (width <= 0 || height <= 0) return;

        var inputs = new List<Input>(actions.Length * 2);
        PointD? last = null;
        foreach (var action in actions)
        {
            if (last != action.Point || action.Kind == MouseReplayKind.Move)
            {
                inputs.Add(Event(Move | Absolute | VirtualDesk,
                    Normalize(action.Point.X, left, width),
                    Normalize(action.Point.Y, top, height)));
                last = action.Point;
            }
            uint flags = action.Kind switch
            {
                MouseReplayKind.LeftDown => LeftDown,
                MouseReplayKind.LeftUp => LeftUp,
                MouseReplayKind.RightDown => RightDown,
                MouseReplayKind.RightUp => RightUp,
                _ => 0u
            };
            if (flags != 0) inputs.Add(Event(flags));
        }
        var array = inputs.ToArray();
        if (generation != Volatile.Read(ref _generation)) return;
        var sent = SendInput((uint)array.Length, array, Marshal.SizeOf<Input>());
        for (var i = 0; i < sent; i++) Track(array[i].Mouse.Flags);
        if (sent != array.Length)
        {
            Debug.WriteLine($"Mouse replay inserted {sent}/{array.Length} events; error={Marshal.GetLastWin32Error()}");
            ReleaseHeld(); // do not retry a partly delivered click
        }
    }

    private void Track(uint flags)
    {
        if ((flags & LeftDown) != 0) _leftDown = true;
        if ((flags & LeftUp) != 0) _leftDown = false;
        if ((flags & RightDown) != 0) _rightDown = true;
        if ((flags & RightUp) != 0) _rightDown = false;
    }

    private void ReleaseHeld()
    {
        var held = new List<Input>(2);
        if (_leftDown) held.Add(Event(LeftUp));
        if (_rightDown) held.Add(Event(RightUp));
        if (held.Count == 0) return;
        var array = held.ToArray();
        var sent = SendInput((uint)array.Length, array, Marshal.SizeOf<Input>());
        for (var i = 0; i < sent; i++) Track(array[i].Mouse.Flags);
    }

    public void Dispose()
    {
        Invalidate();
        _queue.CompleteAdding();
        // The hook is already uninstalled when disposing this lane.
        if (_thread.Join(TimeSpan.FromSeconds(3))) _queue.Dispose();
    }
}
