using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class PhoneCanvasLayoutTests
{
    [Theory]
    [InlineData(1080, 1900, 540, 950)]
    [InlineData(1080, 1834, 540, 917)]
    [InlineData(720, 1223, 540, 917.25)]
    [InlineData(2160, 3668, 540, 917)]
    public void ViewboxUsesStableDesignWidthRegardlessOfVirtualResolution(
        int width, int height, int expectedWidth, double expectedHeight)
    {
        var canvas = PhoneCanvasLayout.ForDisplay(width, height);
        Assert.Equal(expectedWidth, canvas.Width);
        Assert.Equal(expectedHeight, canvas.Height, 2);
        Assert.Equal((double)width / height, canvas.Width / canvas.Height, 8);
    }

    [Theory]
    [InlineData(0, 1834)]
    [InlineData(-1, 1834)]
    [InlineData(1080, 0)]
    [InlineData(1080, -1)]
    public void InvalidDisplayDimensionsAreRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhoneCanvasLayout.ForDisplay(width, height));
    }
}
