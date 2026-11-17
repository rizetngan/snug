using Snug.Windows.Core;
namespace Snug.Windows.Core.Tests;
public sealed class MenuMomentumTests
{
    [Fact] public void FastReleaseCoastsAndDecays()
    {
        var m=new MenuMomentum();m.Begin(1,0);m.Sample(1.05,50);m.Sample(1.09,90);
        Assert.True(m.Release(1.10));
        var first=m.Advance(1d/60);var second=m.Advance(1d/60);
        Assert.InRange(first,15.8,15.9);Assert.True(second<first);
        for(var i=0;i<180;i++)m.Advance(1d/60);
        Assert.False(m.IsCoasting);
    }
    [Fact] public void PauseBeforeReleaseDoesNotFling()
    {var m=new MenuMomentum();m.Begin(1,0);m.Sample(1.05,100);m.Sample(1.09,150);Assert.False(m.Release(1.3));}
    [Fact] public void SlowMovementDoesNotFling()
    {var m=new MenuMomentum();m.Begin(1,0);m.Sample(1.05,2);m.Sample(1.09,4);Assert.False(m.Release(1.1));}
    [Fact] public void ReverseAndCancel()
    {var m=new MenuMomentum();m.Begin(1,100);m.Sample(1.05,40);m.Sample(1.09,0);Assert.True(m.Release(1.1));Assert.True(m.Advance(1d/60)<0);m.Stop();Assert.Equal(0,m.Advance(1d/60));}
    [Fact] public void FrameRateDoesNotChangeTravel()
    {
        MenuMomentum Start(){var m=new MenuMomentum();m.Begin(1,0);m.Sample(1.05,50);m.Sample(1.09,90);m.Release(1.1);return m;}
        var a=Start();var b=Start();double x=0,y=0;
        for(int i=0;i<12;i++)x+=a.Advance(1d/60);
        for(int i=0;i<6;i++)y+=b.Advance(1d/30);
        Assert.Equal(x,y,6);
    }
}