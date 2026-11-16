using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class ScreenFitTests
{
    [Fact]
    public void DefaultIPhoneMatchesTheExisting1080By1900Display()
    {
        var fit = new ScreenFit(PhoneChoice.Standard.Screen);

        Assert.Equal(540, fit.BaseWidth);
        Assert.Equal(55, fit.Widths.Count);
        Assert.Equal(new PhoneSize(540, 950), fit.SizeFor(540));
        Assert.Equal(new PhoneSize(1080, 1900), fit.PixelSizeFor(540));
    }

    [Fact]
    public void WidthToFitChoosesTheNarrowestModeThatContainsTheWindow()
    {
        var fit = new ScreenFit(PhoneChoice.Standard.Screen);

        var width = fit.WidthToFit(new PhoneSize(800, 1200), new PhoneSize(540, 922), 540);

        Assert.Equal(800, width);
        Assert.Equal(540, fit.WidthToFit(new PhoneSize(500, 900), new PhoneSize(540, 922), 540));
    }

    [Theory]
    [InlineData(600, 1500, 540, 922, 540, 880)]
    [InlineData(500, 900, 540, 922, 540, 540)]
    [InlineData(5000, 9000, 540, 922, 540, 1080)]
    [InlineData(950, 1700, 850, 1400, 850, 1040)]
    [InlineData(700, 1000, 850, 1400, 850, 850)]
    public void ModeSelectionHandlesMinimumSizesAndKeepsTheCurrentMode(
        int appWidth, int appHeight, int areaWidth, int areaHeight,
        int current, int expected)
    {
        var fit = ScreenFit.Standard;
        var next = fit.WidthToFit(
            new PhoneSize(appWidth, appHeight),
            new PhoneSize(areaWidth, areaHeight), current);

        Assert.Equal(expected, next);
        Assert.Contains(next, fit.Widths);
        var vddModes = VddResolutionProfile.Create(PhoneChoice.Standard);
        Assert.Single(vddModes);
        Assert.Equal(fit.PixelSizeFor(fit.BaseWidth).Width, vddModes[0].Width);
        // Logical sizing still chooses a fit width without advertising 55 driver modes.
    }

    [Fact]
    public void CurrentModeIsPreservedWhilePhoneDisplayIsUnavailable()
    {
        var fit = ScreenFit.Standard;
        Assert.Equal(850, fit.WidthToFit(new PhoneSize(900, 1500),
            new PhoneSize(0, 0), 850));
    }

    [Fact]
    public void HomeBarAndGestureScaleFollowCurrentLogicalWidth()
    {
        Assert.Equal(28, ScreenFit.HomeBarHeight(540));
        Assert.Equal(56, ScreenFit.HomeBarHeight(1080));
        Assert.Equal(1.5, ScreenFit.Scale(810));
    }

    [Fact]
    public void InvalidPhoneDimensionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ScreenFit(new PhoneScreen(0, 2400)));
    }
}
