namespace Snug.Windows.Core;

/// <summary>Win32 uses opposite wheel signs on the two axes: finger motion
/// down scrolls up; finger motion left scrolls right. Retain both axes.</summary>
public readonly record struct WheelDeltas(short Horizontal, short Vertical)
{
    public static WheelDeltas From(ScrollStep step) => new(
        (short)Math.Clamp(-3L * step.PixelsX, short.MinValue, short.MaxValue),
        (short)Math.Clamp(3L * step.PixelsY, short.MinValue, short.MaxValue));
}