using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class BrowserChromeHitTestTests
{
    [Theory]
    [InlineData("chrome")]
    [InlineData("msedge")]
    [InlineData("firefox")]
    [InlineData("brave")]
    public void NativeBrowserTabsBypassTouchReplay(string browser)
    {
        var window = new WindowBounds(1920, 0, 820, 1349);
        Assert.True(BrowserChromeHitTest.ShouldPassThrough(browser, window, new PointD(2320, 21), 1.52));
        Assert.True(BrowserChromeHitTest.ShouldPassThrough(browser, window, new PointD(2320, 85), 1.52));
        Assert.False(BrowserChromeHitTest.ShouldPassThrough(browser, window, new PointD(2320, 500), 1.52));
    }

    [Fact]
    public void NonBrowserAndOutsideWindowStillUseOriginalGestureTranslator()
    {
        var window = new WindowBounds(1920, 0, 820, 1349);
        Assert.False(BrowserChromeHitTest.ShouldPassThrough("Code", window, new PointD(2320, 21), 1.52));
        Assert.False(BrowserChromeHitTest.ShouldPassThrough("chrome", window, new PointD(1900, 21), 1.52));
        Assert.False(BrowserChromeHitTest.ShouldPassThrough("chrome", window, new PointD(2741, 21), 1.52));
    }
}