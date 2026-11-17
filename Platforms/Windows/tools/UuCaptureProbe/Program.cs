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
        if (args is ["--replay-disconnected", var report]) return CaptureReplay.Run(report);
        if (args is [var mode,var id,var sid,var duration] &&
            mode is "--pilot" or "--pilot-disconnected" &&
            Guid.TryParse(id,out var feedId)&&int.TryParse(duration,out var limit))
            return CapturePilot.Run(feedId,sid,limit,mode=="--pilot-disconnected").GetAwaiter().GetResult();
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
        var contexts = new Dictionary<ulong,(int Ordinal,int Owner,int Adapter,ulong Source,DateTime LastReport,int Frames)>();
        var adapters = new Dictionary<(int Pid,ulong Handle),int>();
        var diagnostics = new Dictionary<(int Pid,ulong Log,ulong Index),int>();
        var observed = new Dictionary<int,long>();
        int nextDiagnostic = 0;
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
            else if(e.ProviderGuid==Dxg&&id is 192 or 193 or 1073 or 1074 or 1075 or 1078)
            {
                observed[id]=observed.GetValueOrDefault(id)+1;
                var utc=e.TimeStamp.ToUniversalTime();
                if(id is 192 or 193)
                {
                    // Separate diagnostic context-index namespace. Never equate
                    // ContextIndex with the kernel ContextPtr or a monitor.
                    if(!CurrentServer(e.ProcessID)){unattributed++;return;}
                    if(!TryNumber(e,"WdLogIndex",out var log)||
                       !TryNumber(e,"ContextIndex",out var index)){dropped++;return;}
                    var key=(e.ProcessID,log,index);
                    if(!diagnostics.TryGetValue(key,out int token)){token=++nextDiagnostic;diagnostics[key]=token;}
                    var kind=id==192?"DiagnosticConnect":"DiagnosticDestroy";
                    counts[kind]=counts.GetValueOrDefault(kind)+1;
                    ulong? status=TryNumber(e,id==192?"Status":"DestroyReason",out var value)?value:null;
                    Write(new{utc,provider="DxgKrnl",eventId=id,kind,serverPid=e.ProcessID,diagnosticContext=token,status});
                    if(id==193)diagnostics.Remove(key);
                    return;
                }
                if(!TryNumber(e,"ContextPtr",out var context)||context==0){dropped++;return;}
                if(id==1073)
                {
                    // A context can be attributed only by a create event whose
                    // process generation belongs to GameViewerServer.
                    if(!CurrentServer(e.ProcessID)){contexts.Remove(context);unattributed++;return;}
                    int adapter=0;
                    if(TryNumber(e,"hAdapter",out var handle)&&handle!=0)
                    {
                        var key=(e.ProcessID,handle);
                        if(!adapters.TryGetValue(key,out adapter)){adapter=adapters.Count+1;adapters[key]=adapter;}
                    }
                    if(!TryNumber(e,"VidPnSourceId",out var vid)){dropped++;return;}
                    var ordinal=++nextContext;
                    contexts[context]=(ordinal,e.ProcessID,adapter,vid,utc,0);
                    counts["OutputDuplicationCreate"]=counts.GetValueOrDefault("OutputDuplicationCreate")+1;
                    Write(new{utc,provider="DxgKrnl",eventId=id,kind="OutputDuplicationCreate",serverPid=e.ProcessID,context=ordinal,adapter,vidPnSourceId=vid});
                    return;
                }
                if(!contexts.TryGetValue(context,out var owner)){unattributed++;return;}
                // A cross-process destruction candidate is useful diagnostic
                // evidence when its pointer matches an attributed create.
                // An unfamiliar header PID is never promoted to session proof.
                if(id!=1078&&e.ProcessID!=owner.Owner&&e.ProcessID!=0&&e.ProcessID!=4){unattributed++;return;}
                if(!CurrentServer(owner.Owner)){unattributed++;return;}
                if(id==1078)
                {
                    counts["OutputDuplicationDestroy"]=counts.GetValueOrDefault("OutputDuplicationDestroy")+1;
                    Write(new{utc,provider="DxgKrnl",eventId=id,kind="OutputDuplicationDestroy",serverPid=owner.Owner,
                        context=owner.Ordinal,adapter=owner.Adapter,vidPnSourceId=owner.Source,eventOrigin=e.ProcessID==owner.Owner?"Server":e.ProcessID is 0 or 4?"Kernel":"OtherProcessCandidate",pendingFrameEvents=owner.Frames});
                    contexts.Remove(context);
                    return;
                }
                // No pixel, cursor, region or frame data is retained. Sample
                // the count of operations on exactly the attributed context.
                owner.Frames++;
                if((utc-owner.LastReport).TotalSeconds>=2)
                {
                    Write(new{utc,provider="DxgKrnl",kind="CaptureActivity",serverPid=owner.Owner,context=owner.Ordinal,
                        adapter=owner.Adapter,vidPnSourceId=owner.Source,frameEvents=owner.Frames});
                    owner.LastReport=utc;owner.Frames=0;
                }
                contexts[context]=owner;
            }
        };
        try
        {
            session.EnableProvider(Wgc,TraceEventLevel.Informational,1,
                new TraceEventProviderOptions{EventIDsToEnable=[1,2]});
            session.EnableProvider(Dxg,TraceEventLevel.Verbose,1,
                new TraceEventProviderOptions{EventIDsToEnable=[192,193,1073,1074,1075,1078]});
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
            Write(new{utc=DateTime.UtcNow,kind="ProbeFinished",counts,observed,unattributed,dropped,eventsLost=source.EventsLost,remainingContexts=contexts.Values.Select(c=>new{context=c.Ordinal,c.Adapter,c.Source,pendingFrameEvents=c.Frames}).ToArray(),
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