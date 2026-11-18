using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;
using Snug.Windows.Services;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        using var hook = new MouseGestureHook();
        // Region remains null: global hook passes every event untouched.
        if (!hook.Start() || !hook.IsRunning) throw new Exception("Start failed");
        var field = typeof(MouseGestureHook).GetField("_inputDispatcher", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var input = (Dispatcher)field.GetValue(hook)!;
        if (input.Thread.ManagedThreadId == Environment.CurrentManagedThreadId)
            throw new Exception("Hook on caller thread");
        using var signal = new ManualResetEventSlim();
        long latency = -1;
        var sw = Stopwatch.StartNew();
        input.BeginInvoke(new Action(() => { latency = sw.ElapsedMilliseconds; signal.Set(); }), DispatcherPriority.Input);
        // Simulate a blocked UI thread without injecting or swallowing mouse events.
        Thread.Sleep(400);
        if (!signal.IsSet || latency < 0 || latency >= 400)
            throw new Exception("Worker stalled with caller");
        Console.WriteLine($"INPUT_WORKER=PASS main={Environment.CurrentManagedThreadId} input={input.Thread.ManagedThreadId} priority={input.Thread.Priority} queued_ms={latency} ui_block_ms=400");
        for (int i = 0; i < 20; i++)
        {
            hook.Stop();
            if (hook.IsRunning) throw new Exception("Stop failed");
            if (!hook.Start() || !hook.IsRunning) throw new Exception("Restart failed");
        }
        hook.Stop();
        hook.Dispose();
        if (hook.IsRunning || input.Thread.IsAlive) throw new Exception("Dispose failed");
        Console.WriteLine("HOOK_LIFECYCLE=PASS 20 restart cycles; worker stopped");
        return 0;
    }
}