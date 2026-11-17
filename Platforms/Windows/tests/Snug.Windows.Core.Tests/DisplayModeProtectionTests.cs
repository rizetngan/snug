using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class DisplayModeProtectionTests
{
    private static ProtectedDisplayMode Monitor(string id = "monitor-a", string name = "DISPLAY1") =>
        new(id, name, true, 0, 0, 1920, 1080, 60, 32, 0, 0, 0);

    [Fact]
    public void UnchangedModeRenumberedDisplayDoesNotDrift()
    {
        var original = Monitor();
        Assert.Empty(DisplayModeProtection.FindDrift([original], [original with { DeviceName = "DISPLAY9" }]));
    }

    [Fact]
    public void EveryRawModeAttributeIsProtected()
    {
        var old = Monitor();
        foreach (var next in new[] { old with { Width = 1280 }, old with { Height = 720 },
            old with { Hertz = 120 }, old with { Orientation = 1 }, old with { X = -100 },
            old with { Y = 100 }, old with { BitsPerPixel = 24 }, old with { FixedOutput = 1 },
            old with { Flags = 2 }, old with { IsPrimary = false } })
            Assert.Single(DisplayModeProtection.FindDrift([old], [next]));
    }

    [Fact]
    public void DisconnectedOrNewDisplaysAreNotOverwritten()
    {
        var old = Monitor();
        var replacement = Monitor("monitor-b");
        Assert.Empty(DisplayModeProtection.FindDrift([old], [replacement]));
    }

    [Fact]
    public void AmbiguousOrMissingIdentityIsNeverUsedForRestore()
    {
        var a = Monitor();
        Assert.Empty(DisplayModeProtection.FindDrift([a], [a with { Width = 1280 }, a with { DeviceName = "DISPLAY2" }]));
        Assert.Empty(DisplayModeProtection.FindDrift([a, a], [a with { Width = 1280 }]));
        Assert.Empty(DisplayModeProtection.FindDrift([a with { DeviceId = "" }], [a with { DeviceId = "", Width = 1280 }]));
    }
}