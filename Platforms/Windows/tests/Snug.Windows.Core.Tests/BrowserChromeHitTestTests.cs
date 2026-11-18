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
    [Theory]
    [InlineData(1920, 0, true)]
    [InlineData(2320, 85, true)]
    [InlineData(2320, 130, true)]
    [InlineData(2320, 131, false)]
    [InlineData(2320, 500, false)]
    [InlineData(2740, 30, false)]
    [InlineData(1919, 30, false)]
    public void GeometryPrefilterKeepsBrowserHeaderBoundary(int x, int y, bool expected)
    {
        var window = new WindowBounds(1920, 0, 820, 1349);
        Assert.Equal(expected, BrowserChromeHitTest.IsHeader(window, new PointD(x, y), 1.52));
        Assert.Equal(expected, BrowserChromeHitTest.ShouldPassThrough("chrome", window, new PointD(x, y), 1.52));
    }
}