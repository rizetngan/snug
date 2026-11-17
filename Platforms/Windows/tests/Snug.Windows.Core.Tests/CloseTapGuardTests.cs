using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class CloseTapGuardTests
{
    private static readonly CloseButtonHit Hit = new(new WindowKey(123, 456),
        new WindowBounds(1920, 0, 720, 1180), new PointD(2620, 15));

    [Fact]
    public void NativeCaptionTapClosesOnlyTheSameWindow()
    {
        var guard = new CloseTapGuard();
        guard.Begin(Hit, 10, 1);
        Assert.Equal(Hit, guard.Release(Hit, Hit.Point, 10.2));
        Assert.False(guard.Tracking);
    }

    [Fact]
    public void ChangedWindowOrMovedFrameDoesNotClose()
    {
        var guard = new CloseTapGuard();
        guard.Begin(Hit, 10, 1);
        Assert.Null(guard.Release(Hit with { Key = new WindowKey(123, 789) }, Hit.Point, 10.2));
        guard.Begin(Hit, 11, 1);
        Assert.Null(guard.Release(Hit with { Frame = new WindowBounds(1921, 0, 720, 1180) }, Hit.Point, 11.2));
    }

    [Fact]
    public void CloseButtonNoLongerHitDoesNotClose()
    {
        var guard = new CloseTapGuard();
        guard.Begin(Hit, 10, 1);
        Assert.Null(guard.Release(null, Hit.Point, 10.2));
    }

    [Fact]
    public void DragAwayAndBackDoesNotClose()
    {
        var guard = new CloseTapGuard();
        guard.Begin(Hit, 10, 1);
        guard.Move(new PointD(2600, 15));
        Assert.Null(guard.Release(Hit, Hit.Point, 10.2));
    }

    [Fact]
    public void LongHoldDoesNotClose()
    {
        var guard = new CloseTapGuard();
        guard.Begin(Hit, 10, 1);
        Assert.Null(guard.Release(Hit, Hit.Point, 10.6));
    }

    [Fact]
    public void ClosingGestureRespectsPhoneScale()
    {
        var guard = new CloseTapGuard();
        guard.Begin(Hit, 10, 2);
        var point = new PointD(2632, 15);
        var moved = Hit with { Point = point };
        Assert.Equal(moved, guard.Release(moved, point, 10.2));
    }
}