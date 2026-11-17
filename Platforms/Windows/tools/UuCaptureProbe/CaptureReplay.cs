using System.Text.Json;
using Snug.Windows.Core;

internal static class CaptureReplay
{
    // Offline-only: the operator explicitly asserts a disconnected starting
    // baseline. Never use this switch to infer the state of a live server.
    public static int Run(string path)
    {
        try
        {
            var rows=File.ReadLines(path).Where(l=>!string.IsNullOrWhiteSpace(l))
                .Select(l=>JsonSerializer.Deserialize<JsonElement>(l)).ToArray();
            string Kind(JsonElement e)=>e.GetProperty("kind").GetString()!;
            DateTimeOffset At(JsonElement e)=>e.GetProperty("utc").GetDateTimeOffset();
            var begin=rows.Single(e=>Kind(e)=="ProbeStarted");
            var end=rows.Single(e=>Kind(e)=="ProbeFinished");
            if(!end.GetProperty("sameServerGeneration").GetBoolean()||
               end.GetProperty("eventsLost").GetInt64()!=0||
               end.GetProperty("dropped").GetInt64()!=0||
               end.GetProperty("unattributed").GetInt64()!=0)
                throw new InvalidDataException("Incomplete trace");
            int pid=begin.GetProperty("serverPids").EnumerateArray().Single().GetInt32();
            if(At(end)<=At(begin))throw new InvalidDataException("Invalid trace time");
            var edges=rows.Where(e=>Kind(e) is "DiagnosticConnect" or "DiagnosticDestroy").ToArray();
            if(edges.Length==0)throw new InvalidDataException("No lifecycle edges");
            // An offline surrogate for the one verified process generation.
            var generation=new UuServerGeneration(pid,At(begin).UtcTicks);
            var capture=new UuCaptureLifecycle();
            capture.Begin(generation,initiallyEmpty:true);
            var tracker=new UuConnectionTracker();
            var legacy=new UuSignals(false,null,null,LogFallback:true);
            var phases=new List<UuCapturePhase>();
            var changes=new List<ConnectionTransitionKind>();
            var lastPhase=UuCapturePhase.Unknown;
            void Tick(DateTimeOffset t)
            {
                var state=capture.Snapshot(t);
                if(state.Phase!=lastPhase)
                {
                    lastPhase=state.Phase;phases.Add(state.Phase);
                    Console.WriteLine($"CAPTURE={state.Phase} at={t:O} count={state.ObservedContexts}");
                }
                // Hold original sticky log hint throughout to prove it cannot
                // prevent expiry after complete, trusted capture teardown.
                foreach(var transition in tracker.Update(UuCaptureLifecycle.Combine(legacy,state),t))
                {
                    changes.Add(transition.Kind);
                    Console.WriteLine($"CONNECTION={transition.Kind} at={t:O}");
                }
            }
            int i=0;
            var active=new HashSet<ulong>();
            for(var t=At(begin);t<=At(end);t=t.AddMilliseconds(500))
            {
                while(i<edges.Length&&At(edges[i])<=t)
                {
                    var e=edges[i++];
                    var opened=Kind(e)=="DiagnosticConnect";
                    if(e.GetProperty("serverPid").GetInt32()!=pid||
                        e.GetProperty("eventId").GetInt32()!=(opened?192:193))
                        throw new InvalidDataException("Unattributed lifecycle edge");
                    var key=e.GetProperty("diagnosticContext").GetUInt64();
                    var status=e.GetProperty("status").GetUInt64();
                    if(opened&&status!=0)throw new InvalidDataException("Failed connect");
                    if(!(opened?active.Add(key):active.Remove(key)))
                        throw new InvalidDataException("Unpaired lifecycle edge");
                    if(!capture.Observe(generation,key,opened,At(e),!opened||status==0))
                        throw new InvalidDataException("Rejected edge");
                    Tick(At(e));
                }
                Tick(t);
            }
            if(i!=edges.Length||active.Count!=0||capture.Snapshot(At(end)).CaptureActive!=false)
                throw new InvalidDataException("Incomplete capture teardown");
            var expectedPhases=new[]{UuCapturePhase.Inactive,UuCapturePhase.Active,UuCapturePhase.Rebuilding,
                UuCapturePhase.Active,UuCapturePhase.Rebuilding,UuCapturePhase.Active,UuCapturePhase.Rebuilding,UuCapturePhase.Inactive};
            if(!phases.SequenceEqual(expectedPhases))throw new InvalidDataException("Unexpected three-context lifecycle");
            if(!changes.SequenceEqual(new[]{ConnectionTransitionKind.Connected,ConnectionTransitionKind.Interrupted,ConnectionTransitionKind.Ended}))
                throw new InvalidDataException("Unexpected connection episode sequence");
            if(tracker.IsConnected||tracker.WantsPhoneScreen(At(end)))throw new InvalidDataException("Phone screen should release");
            Console.WriteLine("REAL_TRACE_REPLAY=PASS edges="+edges.Length+" finalContexts="+active.Count);
            return 0;
        }
        catch(Exception e)
        {
            Console.Error.WriteLine(e.GetType().Name+": "+e.Message);
            return 1;
        }
    }
}