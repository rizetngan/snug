using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class UuConnectionTrackerTests
{
    [Fact]
    public void AnyPositiveSignalConnectsImmediately()
    {
        var tracker = new UuConnectionTracker();
        var now = DateTimeOffset.Parse("2026-11-14T10:00:00Z");

        var events = tracker.Update(new(false, null, true), now);

        Assert.True(tracker.IsConnected);
        Assert.Single(events);
        Assert.Equal(ConnectionTransitionKind.Connected, events[0].Kind);
    }

    [Fact]
    public void SelfInducedDisplayChangeDelaysDisconnect()
    {
        var tracker = new UuConnectionTracker();
        var start = DateTimeOffset.Parse("2026-11-14T10:00:00Z");
        tracker.Update(new(true, null, null), start);
        tracker.NoteSelfInducedChange(start.AddSeconds(1));

        Assert.Empty(tracker.Update(new(false, null, false), start.AddSeconds(4)));
        Assert.True(tracker.IsConnected);

        var events = tracker.Update(new(false, null, false), start.AddSeconds(6.1));
        Assert.Single(events);
        Assert.Equal(ConnectionTransitionKind.Interrupted, events[0].Kind);
    }

    [Fact]
    public void ReconnectWithinWindowResumesSameSession()
    {
        var tracker = new UuConnectionTracker();
        var start = DateTimeOffset.Parse("2026-11-14T10:00:00Z");
        tracker.Update(new(true, null, null), start);
        tracker.Update(new(false, null, false), start.AddSeconds(2));

        var events = tracker.Update(new(true, new("iPhone", ControllerKind.Phone), null), start.AddSeconds(8));

        Assert.Single(events);
        Assert.Equal(ConnectionTransitionKind.Resumed, events[0].Kind);
        Assert.True(tracker.WantsPhoneScreen(start.AddSeconds(8)));
    }


    [Fact]
    public void ContinuousUnknownSessionCreatesPhoneScreenAfterGrace()
    {
        var tracker = new UuConnectionTracker();
        var start = DateTimeOffset.Parse("2026-11-14T10:00:00Z");
        var signal = new UuSignals(false, new("UU Remote", ControllerKind.Unknown), true);

        for (var step = 0; step <= 8; step++)
        {
            var now = start.AddMilliseconds(step * 500);
            tracker.Update(signal, now);
            if (step < 4)
                Assert.False(tracker.WantsPhoneScreen(now));
        }

        Assert.True(tracker.IsConnected);
        Assert.True(tracker.WantsPhoneScreen(start.AddSeconds(4)));
    }

    [Fact]
    public void FinishedUnknownSessionStartsNewGracePeriod()
    {
        var tracker = new UuConnectionTracker();
        var start = DateTimeOffset.Parse("2026-11-14T10:00:00Z");
        var signal = new UuSignals(false, new("UU Remote", ControllerKind.Unknown), true);
        tracker.Update(signal, start);
        Assert.True(tracker.WantsPhoneScreen(start.AddSeconds(3)));

        tracker.Update(new(false, null, false), start.AddSeconds(3));
        tracker.Update(new(false, null, false), start.AddSeconds(20));
        tracker.Update(signal, start.AddSeconds(21));

        Assert.False(tracker.WantsPhoneScreen(start.AddSeconds(21)));
        Assert.True(tracker.WantsPhoneScreen(start.AddSeconds(24)));
    }

    [Fact]
    public void UnknownControllerGetsGracePeriodButDesktopDoesNotCreatePhoneScreen()
    {
        var tracker = new UuConnectionTracker();
        var start = DateTimeOffset.Parse("2026-11-14T10:00:00Z");
        tracker.Update(new(true, null, null), start);

        Assert.False(tracker.WantsPhoneScreen(start.AddSeconds(1)));
        Assert.True(tracker.WantsPhoneScreen(start.AddSeconds(3)));

        var desktop = new UuConnectionTracker();
        desktop.Update(new(true, new("Desktop", ControllerKind.Desktop), null), start);
        Assert.False(desktop.WantsPhoneScreen(start.AddSeconds(10)));
    }
}
