namespace Snug.Windows.Core;

public sealed record PhoneScreenSpec(
    int Width,
    int Height,
    int HomeBarHeight,
    int RefreshRate,
    int PixelScale = 2)
{
    public static PhoneScreenSpec Default { get; } = new(540, 950, 28, 60, 2);

    public int PixelWidth => Width * PixelScale;
    public int PixelHeight => Height * PixelScale;
    public int PixelHomeBarHeight => HomeBarHeight * PixelScale;
    public int ContentPixelHeight => PixelHeight - PixelHomeBarHeight;
    public double AspectRatio => (double)Width / Height;
}