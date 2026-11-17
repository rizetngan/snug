namespace Snug.Windows.Core;

/// <summary>
/// The UI is designed at the upstream Snug 540-point width. The phone monitor
/// changes physical pixel size with the phone model, but the UI layout width
/// must stay fixed; Viewbox performs uniform scaling onto the display.
/// </summary>
public static class PhoneCanvasLayout
{
    public static PhoneSize ForDisplay(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));

        return new PhoneSize(
            ScreenFit.DesignWidth,
            ScreenFit.DesignWidth * (double)pixelHeight / pixelWidth);
    }
}
