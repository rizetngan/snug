using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class PhoneSessionPresentationTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void DisplayLifetimeDependsOnlyOnUuIntentAndSetting(bool wants, bool enabled, bool expected)
    {
        Assert.Equal(expected, PhoneSessionPresentation.ShouldKeepDisplay(wants, enabled));
    }

    [Fact]
    public void RepeatedMonitorSwitchesDoNotEndPhoneDisplay()
    {
        var tracker = new UuConnectionTracker();
        var t = DateTimeOffset.UnixEpoch;
        var phone = new ControllerInfo("test", ControllerKind.Phone);
        for (var i = 0; i < 10; i++)
        {
            // UU's capture can switch targets while the same phone controller stays connected.
            tracker.Update(new UuSignals(false, phone, i % 2 == 0), t.AddSeconds(i));
            Assert.True(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t.AddSeconds(i)), true));
        }
    }

    [Fact]
    public void BriefCaptureInterruptionKeepsDisplayAndSessionResumeDoesNotRequireReentry()
    {
        var tracker = new UuConnectionTracker();
        var t = DateTimeOffset.UnixEpoch;
        var active = new UuSignals(false, new ControllerInfo("test", ControllerKind.Phone), true);
        tracker.Update(active, t);
        var inactive = new UuSignals(false, null, false);
        tracker.Update(inactive, t.AddSeconds(2));
        Assert.False(tracker.IsConnected);
        Assert.True(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t.AddSeconds(2)), true));
        tracker.Update(active, t.AddSeconds(4));
        Assert.True(tracker.IsConnected);
        Assert.True(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t.AddSeconds(4)), true));
    }

    [Fact]
    public void TrueDisconnectReleasesDisplayAndNewPhoneSessionCreatesAgain()
    {
        var tracker = new UuConnectionTracker();
        var t = DateTimeOffset.UnixEpoch;
        var active = new UuSignals(false, new ControllerInfo("test", ControllerKind.Phone), true);
        var inactive = new UuSignals(false, null, false);
        tracker.Update(active, t);
        tracker.Update(inactive, t.AddSeconds(2));
        var ended = tracker.Update(inactive, t.AddSeconds(18));
        Assert.Contains(ended, x => x.Kind == ConnectionTransitionKind.Ended);
        Assert.False(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t.AddSeconds(18)), true));
        tracker.Update(active, t.AddSeconds(20));
        Assert.True(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t.AddSeconds(20)), true));
    }

    [Fact]
    public void DesktopOnlySessionDoesNotCreatePhoneScreen()
    {
        var tracker = new UuConnectionTracker();
        var t = DateTimeOffset.UnixEpoch;
        tracker.Update(new UuSignals(false, new ControllerInfo("test", ControllerKind.Desktop), true), t);
        Assert.False(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t), true));
    }

    [Fact]
    public void ExplicitSettingCanDisableAndEnableDuringTheSameSession()
    {
        var tracker = new UuConnectionTracker();
        var t = DateTimeOffset.UnixEpoch;
        tracker.Update(new UuSignals(false, new ControllerInfo("test", ControllerKind.Phone), true), t);
        Assert.True(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t), true));
        Assert.False(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t), false));
        Assert.True(PhoneSessionPresentation.ShouldKeepDisplay(tracker.WantsPhoneScreen(t), true));
    }
}
