using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Snug.Windows;
using Snug.Windows.Services;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("PNG output path required.");
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int clicks = 0;
        var display = new DisplayEnumerator().GetDisplays().Single(d => d.IsPrimary);
        PhoneReentryWindow? window = null;
        window = new PhoneReentryWindow(() =>
        {
            clicks++;
            window!.Hide();
            return Task.CompletedTask;
        });
        int result = 1;
        window.Present(display);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(480) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                if (!window.IsVisible || !window.Topmost || window.ShowInTaskbar)
                    throw new Exception("Incorrect tool-window visibility.");
                var screen = System.Windows.Forms.Screen.AllScreens.Single(s => s.DeviceName == display.DeviceName);
                var rect = screen.WorkingArea;
                var tl = window.PointToScreen(new System.Windows.Point(0,0));
                var br = window.PointToScreen(new System.Windows.Point(window.ActualWidth,window.ActualHeight));
                if (tl.X < rect.Left || tl.Y < rect.Top || br.X > rect.Right+1 || br.Y > rect.Bottom+1)
                    throw new Exception("Entry outside primary work area: "+tl+" "+br+" "+rect);
                var bmp = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),
                    (int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);
                bmp.Render(window);
                var encoder=new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using(var file=File.Create(args[0])) encoder.Save(file);
                var button=(System.Windows.Controls.Button)window.FindName("EnterButton");
                var peer = new ButtonAutomationPeer(button);
                if (peer.GetName() != "进入手机屏") throw new Exception("Missing accessible name");
                var invoke = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke);
                invoke.Invoke();
                window.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if(clicks != 1 || window.IsVisible) throw new Exception("Click did not hide and invoke exactly once.");
                        window.Present(display);
                        if(!window.IsVisible) throw new Exception("Entry did not reappear.");
                        Console.WriteLine("PRIMARY_WORK_AREA=PASS UIA_INVOKE=PASS REOPEN=PASS");
                        result=0;
                    }
                    catch(Exception e){Console.Error.WriteLine(e);}
                    finally{window.Close();app.Shutdown(result);}
                },DispatcherPriority.ApplicationIdle);
                return;
            }
            catch(Exception e){Console.Error.WriteLine(e);window.Close();app.Shutdown(result);}
        };
        timer.Start();
        app.Run();
        Environment.ExitCode=result;
    }
}