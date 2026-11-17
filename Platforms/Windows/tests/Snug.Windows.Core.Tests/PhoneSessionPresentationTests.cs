using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class PhoneSessionPresentationTests
{
    [Fact]
    public void StartsUndismissedAndRespectsPhoneSessionIntent()
    {
        var state = new PhoneSessionPresentation();
        Assert.True(state.ShouldPresent(true));
        Assert.False(state.ShouldPresent(false));
    }

    [Fact]
    public void DismissalRemainsForTheSameRemoteEpisode()
    {
        var state = new PhoneSessionPresentation();
        state.DismissForEpisode();
        foreach (var transition in new[]
        {
            ConnectionTransitionKind.Interrupted,
            ConnectionTransitionKind.Resumed,
            ConnectionTransitionKind.ControllerChanged,
            ConnectionTransitionKind.Connected
        })
        {
            state.OnTransition(transition);
            Assert.True(state.DismissedInEpisode);
            Assert.False(state.ShouldPresent(true));
        }
    }

    [Fact]
    public void NextEpisodeRestoresThePhoneScreen()
    {
        var state = new PhoneSessionPresentation();
        state.DismissForEpisode();
        state.OnTransition(ConnectionTransitionKind.Ended);
        Assert.False(state.DismissedInEpisode);
        Assert.True(state.ShouldPresent(true));
    }

    [Fact]
    public void ReEnablingPhoneScreenExplicitlyClearsDismissal()
    {
        var state = new PhoneSessionPresentation();
        state.DismissForEpisode();
        state.EnableAgain();
        Assert.True(state.ShouldPresent(true));
    }

    [Theory]
    [InlineData(false,true,true)]
    [InlineData(true,false,true)]
    [InlineData(true,true,false)]
    public void ReentryUnavailableWithoutActiveEnabledPhoneIntent(bool connected,bool wants,bool enabled)
    {
        var state = new PhoneSessionPresentation();
        state.DismissForEpisode();
        Assert.False(state.CanReenter(connected,wants,enabled));
        Assert.False(state.TryReenter(connected,wants,enabled));
        Assert.True(state.DismissedInEpisode);
    }

    [Fact]
    public void ExplicitReentryClearsSameEpisodeDismissalOnlyOnce()
    {
        var state = new PhoneSessionPresentation();
        Assert.False(state.CanReenter(true,true,true));
        state.DismissForEpisode();
        Assert.True(state.CanReenter(true,true,true));
        Assert.True(state.TryReenter(true,true,true));
        Assert.True(state.ShouldPresent(true));
        Assert.False(state.TryReenter(true,true,true));
    }

    [Fact]
    public void EndedClearsReentryAndNextEpisodeCanPresent()
    {
        var state = new PhoneSessionPresentation();
        state.DismissForEpisode();
        state.OnTransition(ConnectionTransitionKind.Ended);
        Assert.False(state.CanReenter(true,true,true));
        Assert.True(state.ShouldPresent(true));
    }

    [Fact]
    public void ReentryAfterExplicitDismissalUsesSameLiveUuEpisode()
    {
        var tracker = new UuConnectionTracker();
        var state = new PhoneSessionPresentation();
        var t = DateTimeOffset.UnixEpoch;
        var phone = new ControllerInfo("test", ControllerKind.Phone);
        var active = new UuSignals(false, phone, true);
        foreach (var transition in tracker.Update(active,t)) state.OnTransition(transition.Kind);
        state.DismissForEpisode();
        Assert.False(state.ShouldPresent(tracker.WantsPhoneScreen(t)));
        Assert.True(state.TryReenter(tracker.IsConnected,tracker.WantsPhoneScreen(t),true));
        Assert.True(state.ShouldPresent(tracker.WantsPhoneScreen(t)));
        state.DismissForEpisode();
        var inactive = new UuSignals(false,null,false);
        foreach (var transition in tracker.Update(inactive,t.AddSeconds(2))) state.OnTransition(transition.Kind);
        Assert.False(state.CanReenter(tracker.IsConnected,tracker.WantsPhoneScreen(t.AddSeconds(2)),true));
        foreach (var transition in tracker.Update(inactive,t.AddSeconds(18))) state.OnTransition(transition.Kind);
        Assert.False(state.DismissedInEpisode);
        foreach (var transition in tracker.Update(active,t.AddSeconds(20))) state.OnTransition(transition.Kind);
        Assert.True(state.ShouldPresent(tracker.WantsPhoneScreen(t.AddSeconds(20))));
    }
}