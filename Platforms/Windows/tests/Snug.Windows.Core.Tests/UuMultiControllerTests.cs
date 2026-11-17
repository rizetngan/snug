using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class UuMultiControllerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-17T00:00:00Z");
    private static readonly ControllerInfo Desktop = new("DESKTOP-001", ControllerKind.Desktop);
    private static readonly ControllerInfo Phone = new("iPhone 13 Pro", ControllerKind.Phone);
    private static readonly UuSignals LogOnly = new(false, new("UU Remote", ControllerKind.Unknown), true);
    private static UuSignals Local(params ControllerInfo[] names) => new(false, null, true, names);

    [Theory]
    [InlineData("iPhone 13 Pro", ControllerKind.Phone)]
    [InlineData("Android", ControllerKind.Phone)]
    [InlineData("Xiaomi Pad", ControllerKind.Phone)]
    [InlineData("华为平板", ControllerKind.Phone)]
    [InlineData("ThinkPad X1", ControllerKind.Desktop)]
    [InlineData("DESKTOP-001", ControllerKind.Desktop)]
    [InlineData("MacBook Pro", ControllerKind.Desktop)]
    [InlineData("Surface Laptop 5", ControllerKind.Desktop)]
    [InlineData("未命名设备", ControllerKind.Unknown)]
    [InlineData("", ControllerKind.Unknown)]
    public void ClassifierFollowsUpstreamNameHints(string name, ControllerKind kind) =>
        Assert.Equal(kind, ControllerClassifier.Classify(name));

    [Fact]
    public void MixedControllersKeepPhoneScreenRegardlessOfOrder()
    {
        var tracker = new UuConnectionTracker();
        tracker.Update(Local(Desktop, Phone), Start);
        Assert.True(tracker.WantsPhoneScreen(Start));
        Assert.Equal(2, tracker.Controllers.Count);
        Assert.Empty(tracker.Update(Local(Phone, Desktop), Start.AddSeconds(1)));
        Assert.True(tracker.WantsPhoneScreen(Start.AddSeconds(1)));
    }

    [Fact]
    public void RecentlyDepartedPhoneStaysWithinUpstreamReconnectWindow()
    {
        var tracker = new UuConnectionTracker();
        tracker.Update(Local(Desktop, Phone), Start);
        var leave = tracker.Update(Local(Desktop), Start.AddSeconds(1));
        Assert.Equal(ConnectionTransitionKind.ControllerChanged, Assert.Single(leave).Kind);
        Assert.True(tracker.WantsPhoneScreen(Start.AddSeconds(10)));
        Assert.False(tracker.WantsPhoneScreen(Start.AddSeconds(17)));
    }

    [Fact]
    public void UnreadableControllerTableDoesNotOverrideKnownDesktop()
    {
        var tracker = new UuConnectionTracker();
        tracker.Update(Local(Desktop), Start);
        Assert.Empty(tracker.Update(LogOnly, Start.AddSeconds(3)));
        Assert.False(tracker.WantsPhoneScreen(Start.AddSeconds(3)));
        Assert.Equal(Desktop, tracker.Controller);
    }

    [Fact]
    public void LogOnlySessionStillSupportsExistingUnknownControllerGrace()
    {
        var tracker = new UuConnectionTracker();
        tracker.Update(LogOnly, Start);
        Assert.False(tracker.WantsPhoneScreen(Start.AddSeconds(1)));
        Assert.True(tracker.WantsPhoneScreen(Start.AddSeconds(3)));
        Assert.Empty(tracker.Controllers);
    }

    [Fact]
    public void NewEpisodeAfterMissedExpiryDoesNotReusePreviousController()
    {
        var tracker = new UuConnectionTracker();
        tracker.Update(Local(Desktop), Start);
        tracker.Update(new(false, null, false), Start.AddSeconds(2));
        var events = tracker.Update(LogOnly, Start.AddSeconds(20));
        Assert.Equal(new[] { ConnectionTransitionKind.Ended, ConnectionTransitionKind.Connected },
            events.Select(e => e.Kind).ToArray());
        Assert.Empty(tracker.Controllers);
        Assert.False(tracker.WantsPhoneScreen(Start.AddSeconds(21)));
        Assert.True(tracker.WantsPhoneScreen(Start.AddSeconds(23)));
    }
}