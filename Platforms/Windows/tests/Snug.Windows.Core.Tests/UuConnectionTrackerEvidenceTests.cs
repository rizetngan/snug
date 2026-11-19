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
    public void CompleteInactiveInventoryEndsAfterFourSeconds()
    {
        var tracker = Interrupt(Verified);
        for (var t = 2.5; t <= 6; t += .5)
        {
            Assert.Empty(tracker.Update(Verified, T.AddSeconds(t)));
            Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(t)));
        }
        Assert.Equal(ConnectionTransitionKind.Ended,
            Assert.Single(tracker.Update(Verified, T.AddSeconds(6.5))).Kind);
        Assert.False(tracker.WantsPhoneScreen(T.AddSeconds(6.5)));
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
        Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(8)));
        Assert.Equal(ConnectionTransitionKind.Ended,
            Assert.Single(tracker.Update(Verified, T.AddSeconds(8.5))).Kind);
    }

    [Fact]
    public void ReturningCaptureWinsOverEarlyExpiry()
    {
        var tracker = Interrupt(Verified);
        tracker.Update(Verified, T.AddSeconds(5.5));
        Assert.Equal(ConnectionTransitionKind.Resumed,
            Assert.Single(tracker.Update(Active, T.AddSeconds(6.5))).Kind);
        Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(6.5)));
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
        Assert.Empty(tracker.Update(Verified, T.AddSeconds(8)));
        var controller = Verified with { Controller = new("test", ControllerKind.Phone) };
        Assert.Equal(ConnectionTransitionKind.Resumed,
            Assert.Single(tracker.Update(controller, T.AddSeconds(9.2))).Kind);
        Assert.True(tracker.WantsPhoneScreen(T.AddSeconds(9.2)));
    }
}
