using Snug.Windows.Core;
namespace Snug.Windows.Core.Tests;

public sealed class WheelDeltasTests
{
    [Theory]
    [InlineData(-40, 0, 120, 0)]
    [InlineData(40, 0, -120, 0)]
    [InlineData(0, -40, 0, -120)]
    [InlineData(0, 40, 0, 120)]
    [InlineData(-40, 20, 120, 60)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(int.MinValue, int.MaxValue, 32767, 32767)]
    public void ConvertsBothAxesWithCorrectSign(int x, int y, int h, int v)
    {
        var actual = WheelDeltas.From(new(x, y, x, y, ScrollPhase.Changed, MomentumPhase.None, new(0, 0)));
        Assert.Equal(h, actual.Horizontal);
        Assert.Equal(v, actual.Vertical);
    }

    [Fact]
    public void HorizontalDragAndFlingRetainX()
    {
        var t = new TouchTranslator();
        t.Handle(new(PointerKind.Press, new(100,100), 1), true);
        var step = t.Handle(new(PointerKind.Move, new(60,100), 1.05), true).Commands.Last().Scroll!.Value;
        Assert.Equal(120, WheelDeltas.From(step).Horizontal);
        t.Handle(new(PointerKind.Move, new(20,100), 1.09), true);
        t.Handle(new(PointerKind.Release, new(20,100), 1.1), true);
        Assert.True(t.IsCoasting);
        Assert.True(WheelDeltas.From(t.MomentumFrame()!.Value).Horizontal > 0);
    }
}