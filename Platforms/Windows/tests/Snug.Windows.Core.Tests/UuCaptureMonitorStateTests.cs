using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class UuCaptureMonitorStateTests
{
    private static readonly DateTimeOffset TraceStart = DateTimeOffset.UnixEpoch.AddSeconds(100);
    private static UuServerGeneration NewServer() =>
        new(42, TraceStart.AddSeconds(1).UtcTicks);

    [Fact]
    public void ServerStartedAfterTraceCanBuildCompleteInventory()
    {
        var monitor = new UuCaptureMonitorState(TraceStart);
        var server = NewServer();

        Assert.True(monitor.Observe(server, 1, 7, true, true, TraceStart.AddSeconds(2)));
        Assert.Equal(UuCapturePhase.Active, monitor.Snapshot(TraceStart.AddSeconds(2)).Phase);

        Assert.True(monitor.Observe(server, 1, 7, false, true, TraceStart.AddSeconds(3)));
        Assert.Equal(UuCapturePhase.Rebuilding, monitor.Snapshot(TraceStart.AddSeconds(4)).Phase);
        Assert.Equal(UuCapturePhase.Inactive,
            monitor.Snapshot(TraceStart.AddSeconds(3).Add(UuCaptureLifecycle.RestartGrace)).Phase);
    }

    [Fact]
    public void LateAttachedServerNeverInventsDisconnectedBaseline()
    {
        var monitor = new UuCaptureMonitorState(TraceStart);
        var server = new UuServerGeneration(42, TraceStart.AddSeconds(-1).UtcTicks);

        monitor.Observe(server, 1, 7, true, true, TraceStart.AddSeconds(1));
        monitor.Observe(server, 1, 7, false, true, TraceStart.AddSeconds(2));

        Assert.Null(monitor.Snapshot(TraceStart.AddSeconds(10)).CaptureActive);
    }

    [Fact]
    public void EventOlderThanServerGenerationInvalidatesInventory()
    {
        var monitor = new UuCaptureMonitorState(TraceStart);
        var server = NewServer();

        Assert.False(monitor.Observe(server, 1, 7, true, true, TraceStart));
        Assert.Null(monitor.Snapshot(TraceStart.AddSeconds(10)).CaptureActive);
    }

    [Fact]
    public void UnmatchedDestroyKeepsInventoryUnknown()
    {
        var monitor = new UuCaptureMonitorState(TraceStart);
        var server = NewServer();

        Assert.False(monitor.Observe(server, 1, 9, false, true, TraceStart.AddSeconds(2)));
        Assert.Null(monitor.Snapshot(TraceStart.AddSeconds(10)).CaptureActive);
    }

    [Fact]
    public void TraceLossInvalidatesUntilNextServerGeneration()
    {
        var monitor = new UuCaptureMonitorState(TraceStart);
        var server = NewServer();
        monitor.Observe(server, 1, 7, true, true, TraceStart.AddSeconds(2));
        monitor.Invalidate();
        monitor.Observe(server, 1, 7, false, true, TraceStart.AddSeconds(3));
        Assert.Null(monitor.Snapshot(TraceStart.AddSeconds(10)).CaptureActive);

        var next = new UuServerGeneration(43, TraceStart.AddSeconds(20).UtcTicks);
        monitor.Observe(next, 1, 1, true, true, TraceStart.AddSeconds(21));
        Assert.True(monitor.Snapshot(TraceStart.AddSeconds(21)).CaptureActive);
    }
}
