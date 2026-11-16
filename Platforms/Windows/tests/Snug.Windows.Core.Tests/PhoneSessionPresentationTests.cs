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
}
