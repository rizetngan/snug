using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class TouchTranslatorTests
{
    [Fact]
    public void TapReplaysOriginalPressAndRelease()
    {
        var translator = new TouchTranslator();
        Assert.False(translator.Handle(new(PointerKind.Press, new(10, 10), 1), true).PassOriginal);

        var response = translator.Handle(new(PointerKind.Release, new(10, 10), 1.1), true);

        Assert.False(response.PassOriginal);
        Assert.Equal(2, response.Commands.Count);
        Assert.Equal(TouchCommandKind.ReplayPress, response.Commands[0].Kind);
        Assert.Equal(TouchCommandKind.ReplayCurrent, response.Commands[1].Kind);
    }

    [Fact]
    public void ShortDragBecomesScroll()
    {
        var translator = new TouchTranslator();
        translator.Handle(new(PointerKind.Press, new(10, 10), 1), true);

        var response = translator.Handle(new(PointerKind.Move, new(10, 35), 1.1), true);

        Assert.False(response.PassOriginal);
        Assert.Equal(2, response.Commands.Count);
        Assert.All(response.Commands, command => Assert.Equal(TouchCommandKind.Scroll, command.Kind));
        Assert.Equal(ScrollPhase.Began, response.Commands[0].Scroll!.Value.Phase);
        Assert.Equal(25, response.Commands[1].Scroll!.Value.PixelsY);
    }

    [Fact]
    public void LongPressReleaseBecomesRightClick()
    {
        var translator = new TouchTranslator();
        translator.Handle(new(PointerKind.Press, new(20, 30), 1), true);

        var response = translator.Handle(new(PointerKind.Release, new(20, 30), 1.6), true);

        Assert.False(response.PassOriginal);
        Assert.Single(response.Commands);
        Assert.Equal(TouchCommandKind.RightClick, response.Commands[0].Kind);
    }

    [Fact]
    public void LongPressThenMoveReplaysPressAndMoveForTextSelection()
    {
        var translator = new TouchTranslator();
        translator.Handle(new(PointerKind.Press, new(20, 30), 1), true);

        var response = translator.Handle(new(PointerKind.Move, new(35, 30), 1.6), true);

        Assert.Equal(TouchCommandKind.ReplayPress, response.Commands[0].Kind);
        Assert.Equal(TouchCommandKind.ReplayCurrent, response.Commands[1].Kind);
        Assert.True(translator.Handle(new(PointerKind.Release, new(35, 30), 1.7), true).PassOriginal);
    }

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(1.6, true)]
    [InlineData(2.0, true)]
    public void TapJitterTracksOriginalSnugScreenScale(double scale, bool shouldTap)
    {
        var translator = new TouchTranslator();
        translator.Handle(new(PointerKind.Press, new(60, 24), 1), true, scale);
        var moved = translator.Handle(new(PointerKind.Move, new(73, 24), 1.08), true, scale);
        var released = translator.Handle(new(PointerKind.Release, new(73, 24), 1.1), true, scale);

        if (shouldTap)
        {
            Assert.Empty(moved.Commands);
            Assert.Equal(2, released.Commands.Count);
            Assert.Equal(TouchCommandKind.ReplayPress, released.Commands[0].Kind);
            Assert.Equal(TouchCommandKind.ReplayCurrent, released.Commands[1].Kind);
        }
        else
        {
            Assert.Contains(moved.Commands, command => command.Kind == TouchCommandKind.Scroll);
            Assert.Single(released.Commands);
            Assert.Equal(TouchCommandKind.Scroll, released.Commands[0].Kind);
        }
    }
    [Fact]
    public void PressOutsidePhoneScreenPassesThrough()
    {
        var translator = new TouchTranslator();

        var response = translator.Handle(new(PointerKind.Press, new(400, 400), 1), false);

        Assert.True(response.PassOriginal);
        Assert.Empty(response.Commands);
    }
}
