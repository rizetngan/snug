using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>Reads the fixed, one-way capture-state feed from the SYSTEM
/// startup monitor. The UI never elevates itself or controls the monitor.</summary>
internal sealed class UuCaptureMonitorReader : IDisposable
{
    private readonly object _gate=new();
    private readonly CancellationTokenSource _stop=new();
    private readonly UuCaptureFeed _feed;
    private readonly Task _reader;
    private StreamWriter? _audit;
    private UuCaptureSnapshot? _lastApplied;

    internal UuCaptureMonitorReader(Guid id)
    {
        _feed=new(id);
        // Optional diagnostic acceptance evidence; disabled in normal use.
        var audit=Environment.GetEnvironmentVariable("SNUG_UU_CAPTURE_PILOT_AUDIT");
        if(!string.IsNullOrWhiteSpace(audit))
        {
            try{_audit=new StreamWriter(new FileStream(audit,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){AutoFlush=true};}
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        _reader=ReadAsync(id,_stop.Token);
    }

    internal static UuCaptureMonitorReader? FromEnvironment()
    {
        if (Guid.TryParse(Environment.GetEnvironmentVariable("SNUG_UU_CAPTURE_PILOT"), out var id) &&
            id != Guid.Empty) return new(id);

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Snug", "uu-capture-monitor.json");
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var currentSid = WindowsIdentity.GetCurrent().User?.Value;
            if (root.GetProperty("schemaVersion").GetInt32() == 1 &&
                string.Equals(root.GetProperty("userSid").GetString(), currentSid, StringComparison.Ordinal) &&
                Guid.TryParse(root.GetProperty("feedId").GetString(), out id) && id != Guid.Empty)
                return new(id);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            JsonException or KeyNotFoundException or InvalidOperationException) { }
        return null;
    }

    internal UuCaptureSnapshot Sample(UuServerGeneration? server)
    {
        lock(_gate)
        {
            var now=DateTimeOffset.UtcNow;
            var applied=_feed.Get(now,server);
            if(_audit is not null&&_lastApplied!=applied)
            {
                _lastApplied=applied;
                try{_audit.WriteLine(JsonSerializer.Serialize(new{utc=now,phase=applied.Phase.ToString(),
                    contexts=applied.ObservedContexts,complete=applied.CompleteInventory}));}
                catch(IOException){try{_audit.Dispose();}catch(IOException){} _audit=null;}
            }
            return applied;
        }
    }

    private async Task ReadAsync(Guid id,CancellationToken stop)
    {
        var buffer = new byte[UuCaptureFrame.Size];
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", UuCaptureFrame.PipeName(id),
                    PipeDirection.In, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
                await pipe.ConnectAsync(15000, stop).ConfigureAwait(false);
                lock (_gate)
                {
                    _feed.Reset();
                    _lastApplied = null;
                }

                while (!stop.IsCancellationRequested)
                {
                    await pipe.ReadExactlyAsync(buffer, stop).ConfigureAwait(false);
                    if (!UuCaptureFrame.TryDecode(buffer, out var frame)) break;
                    lock (_gate)
                        if (!_feed.Accept(frame, DateTimeOffset.UtcNow)) break;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error) when (error is IOException or TimeoutException or
                UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                lock (_gate)
                {
                    _feed.Invalidate();
                    _lastApplied = null;
                }
            }

            if (stop.IsCancellationRequested) break;
            try { await Task.Delay(TimeSpan.FromSeconds(1), stop).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        lock(_gate){try{_audit?.Dispose();}catch(IOException){} _audit=null;}
        // Do not wait synchronously for pipe I/O on the WPF dispatcher.
        _ = _reader.ContinueWith(_=>_stop.Dispose(),TaskScheduler.Default);
    }
}