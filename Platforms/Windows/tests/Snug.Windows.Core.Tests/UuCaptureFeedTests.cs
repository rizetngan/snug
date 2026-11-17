using Snug.Windows.Core;
namespace Snug.Windows.Core.Tests;

public sealed class UuCaptureFeedTests
{
    private static readonly Guid Id=Guid.Parse("13dbb9b9-930c-4522-bf0c-018fbb327645");
    private static readonly DateTimeOffset T=DateTimeOffset.UnixEpoch.AddSeconds(100);
    private static readonly UuServerGeneration Server=new(10,T.AddSeconds(-10).UtcTicks);
    private static UuCaptureFrame Frame(long seq=1,UuCapturePhase phase=UuCapturePhase.Inactive)=>
        new(Id,seq,T,Server,new(phase,phase==UuCapturePhase.Active?1:0,phase!=UuCapturePhase.Unknown));
    [Theory]
    [InlineData(UuCapturePhase.Active)] [InlineData(UuCapturePhase.Rebuilding)]
    [InlineData(UuCapturePhase.Inactive)] [InlineData(UuCapturePhase.Unknown)]
    public void FixedFrameRoundTrips(UuCapturePhase p)
    {
        var f=Frame(phase:p);Assert.True(UuCaptureFrame.TryDecode(f.Encode(),out var read));Assert.Equal(f,read);
    }
    [Fact] public void RejectMalformedFrames()
    {
        var b=Frame().Encode();b[0]=0;Assert.False(UuCaptureFrame.TryDecode(b,out _));
        Assert.False(UuCaptureFrame.TryDecode(b[..20],out _));
        var invalid=Frame() with {Capture=new(UuCapturePhase.Inactive,0,false)};
        Assert.False(UuCaptureFrame.TryDecode(invalid.Encode(),out _));
        invalid=Frame() with {Capture=new(UuCapturePhase.Active,0,true)};
        Assert.False(UuCaptureFrame.TryDecode(invalid.Encode(),out _));
    }
    [Fact] public void StaleOrWrongGenerationDoesNotSuppressFallback()
    {
        var f=new UuCaptureFeed(Id);Assert.True(f.Accept(Frame(),T));
        Assert.False(f.Get(T,Server).CaptureActive);
        Assert.Null(f.Get(T.AddSeconds(4),Server).CaptureActive);
        Assert.Null(f.Get(T,Server with{StartedUtcTicks=Server.StartedUtcTicks+1}).CaptureActive);
        Assert.Null(f.Get(T,null).CaptureActive);
        f.Invalidate();Assert.Null(f.Get(T,Server).CaptureActive);
    }
    [Fact] public void ResetAllowsSequenceRestartAfterMonitorReconnect()
    {
        var f = new UuCaptureFeed(Id);
        Assert.True(f.Accept(Frame(9), T));
        f.Reset();
        Assert.True(f.Accept(Frame(1), T));
        Assert.Equal(UuCapturePhase.Inactive, f.Get(T, Server).Phase);
    }

    [Fact] public void ReorderedAndWrongFeedReject()
    {
        var f=new UuCaptureFeed(Id);Assert.True(f.Accept(Frame(2),T));
        Assert.False(f.Accept(Frame(1),T));Assert.Null(f.Get(T,Server).CaptureActive);
        Assert.False(f.Accept(Frame(3) with{FeedId=Guid.NewGuid()},T));
        Assert.True(f.Accept(Frame(4),T));
        Assert.False(f.Accept(Frame(5) with{ObservedAt=T.AddSeconds(20)},T));
        Assert.Null(f.Get(T,Server).CaptureActive);
    }
}