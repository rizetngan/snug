using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Snug.Windows.Core;

/// <summary>Explicit, bounded pilot. Only sends fixed capture metadata to a
/// caller-specified local user. No privileged operations are requested by
/// the pipe client. No persistent service, ACL change or log file.</summary>
internal static class CapturePilot
{
    private static readonly Guid Provider=new("802ec45a-1e99-4b83-9920-87c98277ba9d");

    internal static async Task<int> Run(Guid feedId,string sidText,int seconds,bool acknowledgedEmpty)
    {
        if(feedId==Guid.Empty||seconds is <5 or >600)return 2;
        if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            return 3;
        var targetSid=new SecurityIdentifier(sidText);
        var security=new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),
            PipeAccessRights.FullControl,AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),
            PipeAccessRights.FullControl,AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(targetSid,PipeAccessRights.Read|PipeAccessRights.Synchronize,AccessControlType.Allow));
        using var pipe=NamedPipeServerStreamAcl.Create(UuCaptureFrame.PipeName(feedId),PipeDirection.Out,1,
            PipeTransmissionMode.Byte,PipeOptions.Asynchronous,0,4096,security);
        using var lifetime=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;lifetime.Cancel();};
        var gate=new object();
        var state=new UuCaptureLifecycle();
        UuServerGeneration? expected=ReadGeneration();
        if(expected is null){Console.Error.WriteLine("Exactly one readable UU server generation required.");return 4;}
        var server=expected.Value;
        var contexts=new Dictionary<(ulong Log,ulong Index),ulong>();
        ulong next=0;
        using var session=new TraceEventSession("Snug-UU-Pilot-"+feedId.ToString("N"))
        {StopOnDispose=true,BufferSizeMB=16};
        var source=session.Source;
        bool healthy=true;
        bool Read(TraceEvent e,string field,out ulong value)
        {
            try
            {
                if(!e.PayloadNames.Contains(field)||e.PayloadByName(field) is not { } raw)
                {value=0;return false;}
                value=Convert.ToUInt64(raw);return true;
            }
            catch{value=0;return false;}
        }
        void Invalidate(){healthy=false;state.Invalidate();}
        source.Dynamic.All+=e=>
        {
            var id=(int)e.ID;
            if(e.ProviderGuid!=Provider||id is not(192 or 193)||e.ProcessID!=server.Pid)return;
            lock(gate)
            {
                if(!healthy)return;
                if(ReadGeneration()!=server||e.Version!=0||
                   !Read(e,"WdLogIndex",out var log)||!Read(e,"ContextIndex",out var index)||
                   !Read(e,id==192?"Status":"DestroyReason",out var status))
                {Invalidate();return;}
                if(id==192&&status!=0)return;
                var key=(log,index);
                if(!contexts.TryGetValue(key,out var token))
                {
                    token=++next;
                    if(id==192)contexts[key]=token;
                }
                var time=new DateTimeOffset(e.TimeStamp.ToUniversalTime());
                state.Observe(server,token,id==192,time);
                if(id==193)contexts.Remove(key);
                if(contexts.Count>1024)Invalidate();
            }
        };
        Task? processing=null;
        try
        {
            // Begin before enabling the provider: no initial-inventory claim
            // without explicit operator acknowledgement of all UU disconnection.
            state.Begin(server,initiallyEmpty:acknowledgedEmpty);
            session.EnableProvider(Provider,TraceEventLevel.Verbose,1,
                new TraceEventProviderOptions{EventIDsToEnable=[192,193]});
            processing=Task.Run(()=>source.Process());
            Console.WriteLine("PILOT_READY");
            await pipe.WaitForConnectionAsync(lifetime.Token);
            long sequence=0;
            while(!lifetime.IsCancellationRequested)
            {
                UuCaptureFrame frame;
                lock(gate)
                {
                    if(processing.IsCompleted||source.EventsLost!=0||ReadGeneration()!=server)Invalidate();
                    var now=DateTimeOffset.UtcNow;
                    frame=new(feedId,++sequence,now,server,state.Snapshot(now));
                }
                await pipe.WriteAsync(frame.Encode(),lifetime.Token);
                await pipe.FlushAsync(lifetime.Token);
                if(!healthy)break;
                await Task.Delay(500,lifetime.Token);
            }
            return 0;
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested){return 0;}
        catch(IOException){return 0;} // client exit: stop the trace
        catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
        finally
        {
            // Stop the owned session itself, including eventless traces.
            try{session.Stop();}catch(Exception e){Console.Error.WriteLine("Stop: "+e.GetType().Name);}
            if(processing is not null)
            {
                try{await processing.WaitAsync(TimeSpan.FromSeconds(5));}
                catch(Exception e){Console.Error.WriteLine("Drain: "+e.GetType().Name);}
            }
        }
    }

    internal static UuServerGeneration? ReadGeneration()
    {
        var processes=Process.GetProcessesByName("GameViewerServer");
        try
        {
            if(processes.Length!=1)return null;
            return new(processes[0].Id,processes[0].StartTime.ToUniversalTime().Ticks/10*10);
        }
        catch(InvalidOperationException){return null;}
        catch(System.ComponentModel.Win32Exception){return null;}
        finally{foreach(var p in processes)p.Dispose();}
    }
}