using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class UuConnectionTrackerEvidenceTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.UnixEpoch;
    private static readonly UuSignals Active = new(false, new("test", ControllerKind.Phone), true);
    private static readonly UuSignals Unknown = new(false, null, null);
    private static readonly UuSignals Verified = new(false, null, false, CaptureVerifiedInactive: true);

    private static UuConnectionTracker Interrupt(UuSignals negative)
    {
        var tracker = new UuConnectionTracker();
        tracker.Update(Active, T);
        Assert.Equal(ConnectionTransitionKind.Interrupted,
            Assert.Single(tracker.Update(negative, T.AddSeconds(2))).Kind);
        return tracker;
    }

    [Fact]
    public void CompleteInactiveInventoryEndsAfterTwoSeconds()
    {
        var tracker = Interrupt(Verified);
        for (var t = 2.5; t <= 4; t += .5)
        {
            Assert.Empty(tracker.Update(Verified, T.AddSeconds(t)));
            Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(t)));
        }
        Assert.Equal(ConnectionTransitionKind.Ended,
            Assert.Single(tracker.Update(Verified, T.AddSeconds(4.5))).Kind);
        Assert.False(tracker.WantsPhoneScreen(T.AddSeconds(4.5)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnverifiedOrLostInventoryRetainsSixSeconds(bool previouslyVerified)
    {
        var tracker = Interrupt(previouslyVerified ? Verified : Unknown);
        tracker.Update(Unknown, T.AddSeconds(4));
        Assert.Empty(tracker.Update(Unknown, T.AddSeconds(6.5)));
        Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(8)));
        Assert.Equal(ConnectionTransitionKind.Ended,
            Assert.Single(tracker.Update(Unknown, T.AddSeconds(8.5))).Kind);
    }

    [Fact]
    public void LateVerifiedEvidenceCannotBackdateEarlyDeadline()
    {
        var tracker = Interrupt(Unknown);
        tracker.Update(Verified, T.AddSeconds(5));
        Assert.Empty(tracker.Update(Verified, T.AddSeconds(6.5)));
        Assert.Empty(tracker.Update(Verified, T.AddSeconds(7)));
        Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(7)));
        Assert.Equal(ConnectionTransitionKind.Ended,
            Assert.Single(tracker.Update(Verified, T.AddSeconds(7.5))).Kind);
    }

    [Fact]
    public void ReturningCaptureWinsOverEarlyExpiry()
    {
        var tracker = Interrupt(Verified);
        tracker.Update(Verified, T.AddSeconds(3.5));
        Assert.Equal(ConnectionTransitionKind.Resumed,
            Assert.Single(tracker.Update(Active, T.AddSeconds(4.5))).Kind);
        Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(4.5)));
    }

    [Fact]
    public void VerifiedFastPathRetainsCaptureRebuildAndDisconnectDebounce()
    {
        var tracker = new UuConnectionTracker();
        var capture = new UuCaptureLifecycle();
        var server = new UuServerGeneration(1, 1);
        capture.Begin(server, true);
        capture.Observe(server, 1, true, T);
        UuSignals Sample(double seconds) =>
            UuCaptureLifecycle.Combine(Unknown, capture.Snapshot(T.AddSeconds(seconds)));
        tracker.Update(Sample(0), T);
        capture.Observe(server, 1, false, T.AddSeconds(1));
        for (double time = 1; time < 4; time += .5)
        {
            Assert.True(Sample(time).IsPresent);
            Assert.Empty(tracker.Update(Sample(time), T.AddSeconds(time)));
        }
        Assert.True(Sample(4).CaptureVerifiedInactive);
        Assert.Empty(tracker.Update(Sample(4), T.AddSeconds(4)));
        Assert.Empty(tracker.Update(Sample(5), T.AddSeconds(5)));
        Assert.Equal(ConnectionTransitionKind.Interrupted,
            Assert.Single(tracker.Update(Sample(5.5), T.AddSeconds(5.5))).Kind);
        Assert.Empty(tracker.Update(Sample(7.5), T.AddSeconds(7.5)));
        Assert.Equal(ConnectionTransitionKind.Ended,
            Assert.Single(tracker.Update(Sample(8), T.AddSeconds(8))).Kind);
    }

    [Fact]
    public void IndependentControllerAndTopologyProtectionPreventEarlyEnd()
    {
        var tracker = new UuConnectionTracker();
        tracker.Update(Active, T);
        tracker.NoteSelfInducedChange(T);
        Assert.Empty(tracker.Update(Verified, T.AddSeconds(4.9)));
        Assert.True(tracker.IsConnected);
        Assert.Equal(ConnectionTransitionKind.Interrupted,
            Assert.Single(tracker.Update(Verified, T.AddSeconds(5.1))).Kind);
        Assert.Empty(tracker.Update(Verified, T.AddSeconds(7)));
        var controller = Verified with { Controller = new("test", ControllerKind.Phone) };
        Assert.Equal(ConnectionTransitionKind.Resumed,
            Assert.Single(tracker.Update(controller, T.AddSeconds(7.2))).Kind);
        Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(7.2)));
    }
}
