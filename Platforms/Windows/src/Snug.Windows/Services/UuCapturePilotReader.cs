using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>Opt-in bounded pilot. The normal UI never elevates itself,
/// starts a privileged service, or changes machine-wide ETW settings.</summary>
internal sealed class UuCapturePilotReader : IDisposable
{
    private readonly object _gate=new();
    private readonly CancellationTokenSource _stop=new();
    private readonly UuCaptureFeed _feed;
    private readonly Task _reader;

    internal UuCapturePilotReader(Guid id)
    {
        _feed=new(id);
        _reader=ReadAsync(id,_stop.Token);
    }

    internal static UuCapturePilotReader? FromEnvironment() =>
        Guid.TryParse(Environment.GetEnvironmentVariable("SNUG_UU_CAPTURE_PILOT"),out var id)&&id!=Guid.Empty
            ? new(id) : null;

    internal UuCaptureSnapshot Sample(UuServerGeneration? server)
    {
        lock(_gate)return _feed.Get(DateTimeOffset.UtcNow,server);
    }

    private async Task ReadAsync(Guid id,CancellationToken stop)
    {
        try
        {
            using var pipe=new NamedPipeClientStream(".",UuCaptureFrame.PipeName(id),
                PipeDirection.In,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(15000,stop).ConfigureAwait(false);
            var buffer=new byte[UuCaptureFrame.Size];
            while(!stop.IsCancellationRequested)
            {
                await pipe.ReadExactlyAsync(buffer,stop).ConfigureAwait(false);
                if(!UuCaptureFrame.TryDecode(buffer,out var frame))break;
                lock(_gate)if(!_feed.Accept(frame,DateTimeOffset.UtcNow))break;
            }
        }
        catch(Exception e) when (e is IOException or TimeoutException or OperationCanceledException
            or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
        finally{lock(_gate)_feed.Invalidate();}
    }

    public void Dispose()
    {
        _stop.Cancel();
        // Do not wait synchronously for pipe I/O on the WPF dispatcher.
        _ = _reader.ContinueWith(_=>_stop.Dispose(),TaskScheduler.Default);
    }
}