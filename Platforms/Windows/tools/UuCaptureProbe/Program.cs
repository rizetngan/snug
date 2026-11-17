using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

internal static class Program
{
    private static readonly Guid Wgc = new("7d0cbd25-390e-524d-8c1e-2a8e846055c0");
    private static readonly Guid Dxg = new("802ec45a-1e99-4b83-9920-87c98277ba9d");

    public static int Main(string[] args)
    {
        if (args.Length != 2 || !int.TryParse(args[0],out var seconds) || seconds is < 5 or > 180)
        {
            Console.Error.WriteLine("Usage: UuCaptureProbe.dll <5..180 seconds> <new output.jsonl>");
            return 2;
        }
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            Console.Error.WriteLine("Administrator permission required for this temporary ETW diagnostic.");
            return 3;
        }
        var output = Path.GetFullPath(args[1]);
        var gate = new object();
        using var file = new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var writer = new StreamWriter(file){AutoFlush=true};
        void Write(object o){lock(gate){writer.WriteLine(JsonSerializer.Serialize(o));}}
        var name = "Snug-UU-Capture-Probe-"+Guid.NewGuid().ToString("N");
        // No ETL file: retain only allowlisted metadata, not frames, input, or messages.
        using var session = new TraceEventSession(name) { StopOnDispose=true, BufferSizeMB=16 };
        var source = session.Source;
        var known = new Dictionary<int,DateTime>();
        foreach (var p in Process.GetProcessesByName("GameViewerServer"))
        {
            using(p){try{known[p.Id]=p.StartTime.ToUniversalTime();}catch{}}
        }
        var counts = new Dictionary<string,int>();
        var contexts = new Dictionary<ulong,int>();
        int nextContext=0,unattributed=0,dropped=0;
        bool TryNumber(TraceEvent e,string field,out ulong value)
        {
            try{if(!e.PayloadNames.Contains(field)){value=0;return false;} value=e.PayloadByName(field) switch { IntPtr v=>unchecked((ulong)v.ToInt64()), UIntPtr v=>v.ToUInt64(), null=>0, var v=>Convert.ToUInt64(v) };return e.PayloadByName(field) is not null;}
            catch{value=0;return false;}
        }
        bool CurrentServer(int pid)
        {
            if (!known.TryGetValue(pid,out var started))return false;
            try{using var p=Process.GetProcessById(pid);return p.ProcessName=="GameViewerServer"&&p.StartTime.ToUniversalTime()==started;}
            catch{return false;}
        }

        source.Dynamic.All += e =>
        {
            int id=(int)e.ID;
            if(e.ProviderGuid==Wgc&&id is 1 or 2)
            {
                if(!TryNumber(e,"processId",out var target)||target>int.MaxValue){dropped++;return;}
                if(!CurrentServer((int)target)){unattributed++;return;}
                var kind=id==1?"CaptureStarted":"CaptureStopped";
                counts[kind]=counts.GetValueOrDefault(kind)+1;
                Write(new{utc=e.TimeStamp.ToUniversalTime(),provider="WGC",eventId=id,kind,serverPid=(int)target});
            }
            else if(e.ProviderGuid==Dxg&&id is 1073 or 1078)
            {
                // Header attribution must match the known server process; never
                // infer ownership from DWM/System activity.
                if(!CurrentServer(e.ProcessID)){unattributed++;return;}
                if(!TryNumber(e,"ContextPtr",out var context)||context==0){dropped++;return;}
                string kind=id==1073?"OutputDuplicationCreate":"OutputDuplicationDestroy";
                if(!contexts.TryGetValue(context,out var ordinal)){ordinal=++nextContext;contexts[context]=ordinal;}
                ulong? display=id==1073&&TryNumber(e,"VidPnSourceId",out var vid)?vid:null;
                counts[kind]=counts.GetValueOrDefault(kind)+1;
                Write(new{utc=e.TimeStamp.ToUniversalTime(),provider="DxgKrnl",eventId=id,kind,serverPid=e.ProcessID,context=ordinal,vidPnSourceId=display});
                if(id==1078)contexts.Remove(context);
            }
        };
        try
        {
            session.EnableProvider(Wgc,TraceEventLevel.Informational,1,
                new TraceEventProviderOptions{EventIDsToEnable=[1,2]});
            session.EnableProvider(Dxg,TraceEventLevel.Verbose,1,
                new TraceEventProviderOptions{EventIDsToEnable=[1073,1078]});
            Write(new{utc=DateTime.UtcNow,kind="ProbeStarted",session=name,seconds,serverPids=known.Keys.ToArray()});
            Console.WriteLine($"READY: {seconds}s. Output: {output}");
                        int stopping=0;
            void Stop()
            {
                if(Interlocked.Exchange(ref stopping,1)!=0)return;
                try{session.Stop();}catch(Exception ex){Write(new{utc=DateTime.UtcNow,kind="StopError",type=ex.GetType().Name});}
            }
            using var timer=new Timer(_=>Stop(),null,TimeSpan.FromSeconds(seconds),Timeout.InfiniteTimeSpan);
            Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;Stop();};
            source.Process();
            // A restarted server is not treated as the same process generation.
            Write(new{utc=DateTime.UtcNow,kind="ProbeFinished",counts,unattributed,dropped,eventsLost=source.EventsLost,
                sameServerGeneration=known.Count>0&&known.Keys.All(CurrentServer)});
            return 0;
        }
        catch(Exception ex)
        {
            Write(new{utc=DateTime.UtcNow,kind="ProbeError",type=ex.GetType().Name,hresult=ex.HResult});
            Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);
            return 1;
        }
    }
}