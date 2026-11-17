using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using Snug.Windows.Core;
using Snug.Windows.Services;

internal static class Program
{
    private static UuServerGeneration GetServer() =>
        UuServerIdentity.ReadSingle() ?? throw new InvalidOperationException("Expected a single readable UU server");
    [STAThread] static int Main(string[] args)
    {
        try{return (args is ["--transport",var id]&&Guid.TryParse(id,out var guid)
                ? Transport(guid) : VerifyDetector()).GetAwaiter().GetResult();}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }

    private static async Task<int> Transport(Guid id)
    {
        if(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Receiver must be a standard user");
        using var pipe=new NamedPipeClientStream(".",UuCaptureFrame.PipeName(id),PipeDirection.In,
            PipeOptions.Asynchronous,TokenImpersonationLevel.Identification);
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(18));
        await pipe.ConnectAsync(15000,stop.Token);
        var feed=new UuCaptureFeed(id);
        var buffer=new byte[UuCaptureFrame.Size];
        var server=GetServer();
        for(int i=0;i<3;i++)
        {
            await pipe.ReadExactlyAsync(buffer,stop.Token);
            if(!UuCaptureFrame.TryDecode(buffer,out var frame)||frame.Server!=server||
                !feed.Accept(frame,DateTimeOffset.UtcNow))
                throw new InvalidOperationException("Invalid elevated server heartbeat");
            var snapshot=feed.Get(DateTimeOffset.UtcNow,server);
            // A late attach must remain unknown if no open has actually been observed.
            if(snapshot.Phase==UuCapturePhase.Inactive)
                throw new InvalidOperationException("Invented inactive baseline");
            Console.WriteLine($"LIVE_HEARTBEAT={frame.Sequence} phase={snapshot.Phase}");
        }
        Console.WriteLine("ELEVATED_TO_STANDARD_USER_PIPE=PASS");
        return 0;
    }

    private static async Task<int> VerifyDetector()
    {
        var id=Guid.NewGuid();var server=GetServer();long seq=0;
        // Explicit test fixture only. This separate process constructs the
        // actual detector but no AppController, so it cannot move windows,
        // manage displays, or update the user's Snug settings.
        using var pipe=new NamedPipeServerStream(UuCaptureFrame.PipeName(id),PipeDirection.Out,1,
            PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
        var previous=Environment.GetEnvironmentVariable("SNUG_UU_CAPTURE_PILOT");
        Environment.SetEnvironmentVariable("SNUG_UU_CAPTURE_PILOT",id.ToString());
        try
        {
            using var detector=new UuSessionDetector();
            using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await pipe.WaitForConnectionAsync(limit.Token);
            async Task Stage(UuCapturePhase p,bool complete,bool? desired)
            {
                var until=DateTimeOffset.UtcNow.AddSeconds(4);
                while(DateTimeOffset.UtcNow<until)
                {
                    var frame=new UuCaptureFrame(id,++seq,DateTimeOffset.UtcNow,server,
                        new(p,p==UuCapturePhase.Active?1:0,complete));
                    await pipe.WriteAsync(frame.Encode(),limit.Token);
                    await pipe.FlushAsync(limit.Token);
                    await Task.Delay(120,limit.Token);
                    var s=detector.Sample();
                    if(s.CaptureActive==desired&&
                       (p!=UuCapturePhase.Inactive||!s.LogFallback)&&
                       detector.LastSignalSummary.StartsWith(p switch{
                           UuCapturePhase.Active=>"ETW 捕获中",
                           UuCapturePhase.Rebuilding=>"ETW 捕获重建中",
                           UuCapturePhase.Inactive=>"ETW 捕获已停止",
                           _=>"ETW 未知"}))
                    {Console.WriteLine("DETECTOR_FIXTURE="+p+" capture="+s.CaptureActive+" log="+s.LogFallback);return;}
                }
                throw new InvalidOperationException("Detector did not reach "+p+" : "+detector.LastSignalSummary);
            }
            await Stage(UuCapturePhase.Inactive,true,false);
            await Stage(UuCapturePhase.Active,true,true);
            await Stage(UuCapturePhase.Rebuilding,true,true);
            await Stage(UuCapturePhase.Inactive,true,false);
            await Task.Delay(3400,limit.Token);
            var fallback=detector.Sample();
            if(!detector.LastSignalSummary.StartsWith("ETW 未知")||fallback.CaptureActive==true)
                throw new InvalidOperationException("Stale pilot not invalidated");
            Console.WriteLine("DETECTOR_STALE_FALLBACK=PASS");
            Console.WriteLine("ACTUAL_DETECTOR_PIPE_FIXTURE=PASS");
            return 0;
        }
        finally{Environment.SetEnvironmentVariable("SNUG_UU_CAPTURE_PILOT",previous);}
    }
}