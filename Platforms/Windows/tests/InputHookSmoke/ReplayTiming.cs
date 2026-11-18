using System.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Snug.Windows.Core;
using Snug.Windows.Services;
using Forms = System.Windows.Forms;

internal static class ReplayTiming
{
    private const uint TestMarker = 0x54455354;
    private const uint ReplayMarker = 0x534E5547;
    private const int Down = 0x201, Up = 0x202, Double = 0x203,
        RDown = 0x204, RUp = 0x205, Wheel = 0x20A, HWheel = 0x20E;

    internal static int Run(string[] args)
    {
        if (args[0] == "--target") return Target(args[1]);
        return Verify();
    }

    private sealed class Surface : Forms.Form
    {
        private readonly string _dir;
        internal Surface(string dir)
        {
            _dir = dir;
            Text = "Snug input timing — isolated test";
            StartPosition = Forms.FormStartPosition.Manual;
            Location = new System.Drawing.Point(240, 160);
            ClientSize = new System.Drawing.Size(450, 420);
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = System.Drawing.Color.White;
            SetStyle(Forms.ControlStyles.StandardClick | Forms.ControlStyles.StandardDoubleClick, true);
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            File.WriteAllText(Path.Combine(_dir, "ready.txt"), Handle.ToInt64().ToString());
        }
        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg is Down or Up or Double or RDown or RUp or Wheel or HWheel)
            {
                File.AppendAllText(Path.Combine(_dir, "window.csv"),
                    $"{Stopwatch.GetTimestamp()},{m.Msg:X}{Environment.NewLine}");
            }
            base.WndProc(ref m);
        }
    }

    private static int Target(string dir)
    {
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        Forms.Application.Run(new Surface(dir));
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData { public Point Point; public uint Data,Flags,Time; public UIntPtr Extra; }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point pt);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point pt);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr h,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly record struct Row(long Tick,int Message,uint Marker,long Result,double ChainMs);
    private sealed class Observer : IDisposable
    {
        private readonly Rect _r;
        private readonly Thread _thread;
        private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HookProc _proc;
        private IntPtr _hook;
        internal ConcurrentQueue<Row> Rows {get;} = new();
        internal Observer(Rect r)
        {
            _r = r; _proc=Handle;
            _thread = new Thread(() =>
            {
                SetThreadDpiAwarenessContext(new IntPtr(-4));
                _hook=SetWindowsHookEx(14,_proc,GetModuleHandle(null),0);
                if(_hook==IntPtr.Zero) { _ready.SetException(new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error())); return; }
                _ready.SetResult(Dispatcher.CurrentDispatcher);
                try { Dispatcher.Run(); }
                finally { UnhookWindowsHookEx(_hook); _hook=IntPtr.Zero; }
            }) { IsBackground=true, Name="Snug replay test observer" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            _ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        private IntPtr Handle(int code,IntPtr message,IntPtr data)
        {
            int msg=message.ToInt32();
            if(code<0 || msg is not (Down or Up or RDown or RUp))
                return CallNextHookEx(_hook,code,message,data);
            var d=Marshal.PtrToStructure<MouseData>(data);
            uint marker=unchecked((uint)d.Extra.ToUInt64());
            if((marker!=TestMarker && marker!=ReplayMarker) ||
               d.Point.X<_r.Left || d.Point.X>=_r.Right || d.Point.Y<_r.Top || d.Point.Y>=_r.Bottom)
                return CallNextHookEx(_hook,code,message,data);
            long at=Stopwatch.GetTimestamp();
            var result=CallNextHookEx(_hook,code,message,data);
            Rows.Enqueue(new(at,msg,marker,result.ToInt64(),Ms(Stopwatch.GetTimestamp()-at)));
            return result;
        }
        public void Dispose()
        {
            if(_ready.Task.IsCompletedSuccessfully)
                _ready.Task.Result.BeginInvokeShutdown(DispatcherPriority.Send);
            _thread.Join(TimeSpan.FromSeconds(3));
        }
    }
    private static double Ms(long ticks)=> ticks*1000.0/Stopwatch.Frequency;
    private static void Ensure(bool condition,string problem)
    {
        if(!condition) throw new Exception(problem);
    }
    private static void Move(int x,int y)
    {
        int NX(int v,int origin,int extent)=>(int)Math.Clamp(Math.Floor(((v-origin)*65536d+32768)/extent),0,65535);
        mouse_event(0xC001,(uint)NX(x,GetSystemMetrics(76),GetSystemMetrics(78)),
            (uint)NX(y,GetSystemMetrics(77),GetSystemMetrics(79)),0,new UIntPtr(TestMarker));
    }
    private static void Button(uint flags)=>mouse_event(flags,0,0,0,new UIntPtr(TestMarker));
    private static (long Tick,int Message)[] WindowRows(string dir)=>
        File.Exists(Path.Combine(dir,"window.csv"))
            ? File.ReadAllLines(Path.Combine(dir,"window.csv")).Select(line=>line.Split(','))
                .Where(parts=>parts.Length==2).Select(parts=>(long.Parse(parts[0]),Convert.ToInt32(parts[1],16))).ToArray()
            : [];
    private static void WaitFor(Func<bool> test,int timeout=3000)
    {
        var sw=Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds<timeout)
        { if(test()) return; Thread.Sleep(10); }
        throw new Exception("Timed out waiting for test output");
    }
    private static int Verify()
    {
        SetThreadDpiAwarenessContext(new IntPtr(-4));
        GetCursorPos(out var previousCursor);
        var previousForeground=GetForegroundWindow();
        var dir=Path.Combine(Path.GetTempPath(),"SnugReplaySmoke-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Console.WriteLine("TEST_LOG="+dir);
        var targetInfo=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false };
        targetInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        targetInfo.ArgumentList.Add("--target"); targetInfo.ArgumentList.Add(dir);
        using var target=Process.Start(targetInfo)!;
        IntPtr hwnd=IntPtr.Zero;
        try
        {
            WaitFor(()=>File.Exists(Path.Combine(dir,"ready.txt")) && File.ReadAllText(Path.Combine(dir,"ready.txt")).Length>0,5000);
            hwnd=new IntPtr(long.Parse(File.ReadAllText(Path.Combine(dir,"ready.txt"))));
            Ensure(GetWindowRect(hwnd,out var r),"Target bounds missing");
            int x=r.Left+(r.Right-r.Left)/2, y=r.Top+(r.Bottom-r.Top)/2;
            Ensure(GetAncestor(WindowFromPoint(new Point{X=x,Y=y}),2)==hwnd,"Test window covered");
            using var hook=new MouseGestureHook();
            var type=typeof(MouseGestureHook);
            var regionType=type.GetNestedType("GestureRegion",BindingFlags.NonPublic)!;
            var region=Activator.CreateInstance(regionType,new object[]{new WindowBounds(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top),1.0})!;
            type.GetField("_region",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(hook,region);
            Ensure(hook.Start(),"Cannot start hook");
            using var observer=new Observer(r);
            Thread.Sleep(250);
            for(int i=0;i<6;i++)
            {
                Ensure(GetAncestor(WindowFromPoint(new Point{X=x,Y=y}),2)==hwnd,"Test window covered; stop");
                Move(x,y); Thread.Sleep(40); Button(2); Thread.Sleep(65); Button(4);
                int expected=i+1;
                WaitFor(()=>WindowRows(dir).Count(row=>row.Message==Up)>=expected);
                Thread.Sleep(220);
            }
            var rows=observer.Rows.OrderBy(row=>row.Tick).ToArray();
            var original=rows.Where(row=>row.Marker==TestMarker&&row.Message==Up).ToArray();
            var replayed=rows.Where(row=>row.Marker==ReplayMarker&&row.Message==Up).ToArray();
            var actual=WindowRows(dir).Where(row=>row.Message==Up).ToArray();
            Ensure(original.Length==6 && replayed.Length==6 && actual.Length==6,
                $"tap count mismatch {original.Length}/{replayed.Length}/{actual.Length}");
            for(int i=0;i<6;i++)
            {
                var chain=original[i].ChainMs;
                var replay=Ms(replayed[i].Tick-original[i].Tick);
                var delivered=Ms(actual[i].Tick-original[i].Tick);
                Console.WriteLine($"TAP{i+1} chain_ms={chain:F2} replay_ms={replay:F2} window_ms={delivered:F2} hook_return={original[i].Result}");
                Ensure(original[i].Result==1,"Original release escaped hook");
                Ensure(chain<100 && replay<150 && delivered<200,"Replay delay above smoke gate");
            }
            Ensure(WindowRows(dir).Count(row=>row.Message==Double)>=1, "Double-click not delivered");
            Console.WriteLine("DOUBLE_CLICK=PASS");
            // Long-press, native drag selection and ordinary scrolling.
            Move(x,y); Thread.Sleep(30); Button(2); Thread.Sleep(620); Button(4);
            WaitFor(()=>WindowRows(dir).Any(row=>row.Message==RUp));
            Console.WriteLine("RIGHT_CLICK=PASS");
            Move(x,y); Thread.Sleep(30); Button(2); Thread.Sleep(620);
            Move(x+48,y); Thread.Sleep(80); Move(x+80,y); Thread.Sleep(50); Button(4);
            WaitFor(()=>WindowRows(dir).Count(row=>row.Message==Up)==7);
            Console.WriteLine("SELECTION_RELEASE=PASS");
            Move(x,y); Thread.Sleep(30); Button(2); Thread.Sleep(50);
            Move(x,y+70); Thread.Sleep(70); Move(x,y+100); Thread.Sleep(150); Button(4);
            WaitFor(()=>WindowRows(dir).Any(row=>row.Message==Wheel));
            Ensure(WindowRows(dir).Count(row=>row.Message==Up)==7,"Scroll accidentally clicked");
            Console.WriteLine("SCROLL=PASS");
            // Interrupt a selecting gesture, simulating return to library or
            // a display teardown. A synthetic held button must be released.
            Move(x,y); Thread.Sleep(300); Button(2); Thread.Sleep(620);
            Move(x+48,y);
            WaitFor(()=>WindowRows(dir).Count(row=>row.Message is Down or Double)==8);
            hook.Stop();
            WaitFor(()=>WindowRows(dir).Count(row=>row.Message==Up)==8);
            Ensure(!hook.IsRunning,"Hook stop failed");
            Console.WriteLine("CANCEL_HELD_SELECTION=PASS");
            Console.WriteLine("REPLAY_SMOKE=PASS");
            return 0;
        }
        finally
        {
            if(!target.HasExited){target.CloseMainWindow();if(!target.WaitForExit(1500))target.Kill();}
            if(previousForeground!=IntPtr.Zero)SetForegroundWindow(previousForeground);
            SetCursorPos(previousCursor.X,previousCursor.Y);
        }
    }
}