using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Snug.Windows.Core;

internal static class CaptureMonitor
{
    private static readonly Guid Provider = new("802ec45a-1e99-4b83-9920-87c98277ba9d");
    private static readonly object LogGate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Snug", "Logs", "uu-capture-monitor.log");

    internal static async Task<int> Run(Guid feedId, string sidText)
    {
        if (feedId == Guid.Empty) return 2;
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator)) return 3;

        SecurityIdentifier targetSid;
        try { targetSid = new SecurityIdentifier(sidText); }
        catch (ArgumentException) { return 2; }

        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            targetSid, PipeAccessRights.Read | PipeAccessRights.Synchronize,
            AccessControlType.Allow));

        DateTimeOffset traceStartedAt = default;
        UuCaptureMonitorState? state = null;
        var gate = new object();
        var fatal = 0;
        long sequence = 0;

        using var session = new TraceEventSession("Snug-UU-Monitor-" + Guid.NewGuid().ToString("N"))
        {
            StopOnDispose = true,
            BufferSizeMB = 16
        };
        var source = session.Source;
        Task? processing = null;

        bool Read(TraceEvent e, string field, out ulong value)
        {
            try
            {
                if (!e.PayloadNames.Contains(field) || e.PayloadByName(field) is not { } raw)
                {
                    value = 0;
                    return false;
                }
                value = Convert.ToUInt64(raw);
                return true;
            }
            catch
            {
                value = 0;
                return false;
            }
        }

        source.Dynamic.All += e =>
        {
            var eventId = (int)e.ID;
            if (e.ProviderGuid != Provider || eventId is not (192 or 193)) return;
            try
            {
                var server = ReadSingleServer(e.ProcessID);
                if (server is null) return;

                lock (gate)
                {
                    if (Volatile.Read(ref fatal) != 0) return;
                    if (source.EventsLost != 0)
                    {
                        state!.Invalidate();
                        Interlocked.Exchange(ref fatal, 1);
                        return;
                    }
                    if (e.Version != 0 ||
                        !Read(e, "WdLogIndex", out var log) ||
                        !Read(e, "ContextIndex", out var index))
                    {
                        state!.Invalidate();
                        Log("Capture event schema was not recognized; this server generation is now unknown.");
                        return;
                    }

                    var opened = eventId == 192;
                    var successful = true;
                    if (opened)
                    {
                        if (!Read(e, "Status", out var status))
                        {
                            state!.Invalidate();
                            Log("Capture start event was incomplete; this server generation is now unknown.");
                            return;
                        }
                        successful = status == 0;
                    }
                    else if (!Read(e, "DestroyReason", out _))
                    {
                        state!.Invalidate();
                        Log("Capture destroy event was incomplete; this server generation is now unknown.");
                        return;
                    }

                    state!.Observe(server.Value, log, index, opened, successful,
                        new DateTimeOffset(e.TimeStamp.ToUniversalTime()));

                    if (state!.Snapshot(DateTimeOffset.UtcNow).ObservedContexts > 1024)
                    {
                        state!.Invalidate();
                        Interlocked.Exchange(ref fatal, 1);
                    }
                }
            }
            catch (Exception error)
            {
                lock (gate) state!.Invalidate();
                Log("Capture event processing failed: " + error.GetType().Name);
            }
        };

        try
        {
            session.EnableProvider(Provider, TraceEventLevel.Verbose, 1,
                new TraceEventProviderOptions { EventIDsToEnable = [192, 193] });
            // This timestamp is deliberately after provider enablement. A server
            // that predates it cannot be assigned an invented empty baseline.
            traceStartedAt = DateTimeOffset.UtcNow;
            state = new UuCaptureMonitorState(traceStartedAt);
            var monitorProcess = Process.GetCurrentProcess();
            var heartbeatGeneration = new UuServerGeneration(monitorProcess.Id,
                monitorProcess.StartTime.ToUniversalTime().Ticks / 10 * 10);
            processing = Task.Run(() => source.Process());
            Log("Monitor started; ETW is active and waiting for UU server events.");

            while (true)
            {
                using var pipe = NamedPipeServerStreamAcl.Create(
                    UuCaptureFrame.PipeName(feedId), PipeDirection.Out, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous,
                    0, 4096, security);
                try
                {
                    var connection = pipe.WaitForConnectionAsync();
                    while (!connection.IsCompleted)
                    {
                        if (processing.IsCompleted || Volatile.Read(ref fatal) != 0 || source.EventsLost != 0)
                            throw new InvalidOperationException("ETW monitor health check failed while waiting for Snug.");
                        await Task.Delay(500);
                    }
                    await connection;
                    Log("Snug UI connected to the local capture feed.");

                    while (pipe.IsConnected)
                    {
                        if (processing.IsCompleted)
                            throw new InvalidOperationException("ETW event processing stopped.");
                        if (Volatile.Read(ref fatal) != 0 || source.EventsLost != 0)
                            throw new InvalidOperationException("ETW event loss invalidated the capture inventory.");

                        var now = DateTimeOffset.UtcNow;
                        UuCaptureFrame? frame = null;
                        var actualServer = CapturePilot.ReadGeneration();
                        lock (gate)
                        {
                            if (state!.Generation is { } known && actualServer != known)
                                state!.Invalidate();
                            var generation = state!.Generation ?? heartbeatGeneration;
                            var snapshot = state.Generation is null
                                ? new UuCaptureSnapshot(UuCapturePhase.Unknown, 0, false)
                                : state.Snapshot(now);
                            frame = new(feedId, ++sequence, now, generation, snapshot);
                        }

                        if (frame is { } value)
                        {
                            var bytes = value.Encode();
                            await pipe.WriteAsync(bytes);
                            await pipe.FlushAsync();
                        }
                        await Task.Delay(500);
                    }
                }
                catch (IOException)
                {
                    Log("Snug UI disconnected; ETW observation remains active.");
                }
            }
        }
        catch (Exception error)
        {
            Log("Monitor stopped: " + error.GetType().Name + ": " + error.Message);
            return 1;
        }
        finally
        {
            try { session.Stop(); } catch { }
            if (processing is not null)
            {
                try { await processing.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            }
        }
    }

    private static UuServerGeneration? ReadSingleServer(int eventPid)
    {
        var processes = Process.GetProcessesByName("GameViewerServer");
        try
        {
            if (processes.Length != 1 || processes[0].Id != eventPid) return null;
            return new UuServerGeneration(processes[0].Id,
                processes[0].StartTime.ToUniversalTime().Ticks / 10 * 10);
        }
        catch (InvalidOperationException) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static void Log(string message)
    {
        lock (LogGate)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(directory);
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 1024 * 1024)
                    File.Move(LogPath, LogPath + ".1", true);
                File.AppendAllText(LogPath,
                    DateTimeOffset.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
