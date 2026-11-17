using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class QuitStalledNoticeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-17T00:00:00Z");
    private static readonly QuitProcessIdentity Original = new(8123, 123456789, @"C:\Apps\Editor.exe");
    private static readonly QuitStalledNotice Notice = new("Editor", Original, Now.AddMinutes(2));

    [Fact]
    public void SameProcessWithinFollowWindowIsEligible()
    {
        Assert.True(Notice.CanForce(Original with { ExecutablePath = @"c:\apps\editor.exe" }, Now.AddSeconds(30)));
    }

    [Fact]
    public void RecycledPidCannotAuthorizeForceQuit()
    {
        Assert.False(Notice.CanForce(Original with { StartedUtcTicks = Original.StartedUtcTicks + 1 }, Now));
    }

    [Fact]
    public void DifferentPathOrPidCannotAuthorizeForceQuit()
    {
        Assert.False(Notice.CanForce(Original with { ExecutablePath = @"C:\Other\Editor.exe" }, Now));
        Assert.False(Notice.CanForce(Original with { ProcessId = 8124 }, Now));
    }

    [Fact]
    public void ExitedProcessAndExpiredNoticeCannotBeForced()
    {
        Assert.False(Notice.CanForce(null, Now));
        Assert.False(Notice.CanForce(Original, Now.AddMinutes(3)));
    }
}