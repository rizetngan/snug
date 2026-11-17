using Snug.Windows.Core;
namespace Snug.Windows.Core.Tests;
public sealed class ScreenshotTests
{
    sealed class Fake : IScreenshotService {
        public CapturedImage Image = new([1,2],1600,900);
        public List<CapturedImage> Copies = [];
        public bool CopyOk=true;
        public Task<CapturedImage?> CaptureAsync(WindowKey key)=>Task.FromResult<CapturedImage?>(Image);
        public CapturedImage? Crop(CapturedImage image,WindowBounds b)=>b.Width>0&&b.Height>0 ? new([3],b.Width,b.Height):null;
        public bool Copy(CapturedImage image){if(!CopyOk)return false;Copies.Add(image);return true;}
    }
    [Fact] public async Task CropCopiesOriginalThenSelection()
    {
        var f=new Fake();var s=new Screenshots(f);
        await s.TakeAsync(new(1,2),()=>true);
        Assert.Equal(ShotKind.Preview,s.Overlay!.Kind);Assert.Single(f.Copies);
        s.OpenCrop();Assert.True(s.IsCropping);s.FinishCrop(new(.25,.25,.5,.5));
        Assert.Equal(800,f.Copies.Last().Width);Assert.Equal(450,f.Copies.Last().Height);
        Assert.Equal(ShotNotice.Cropped,s.Overlay!.Notice);
    }
    [Fact] public async Task CancelRetainsFullCopyAndPreviewExpires()
    {
        var now=DateTimeOffset.UtcNow;var f=new Fake();var s=new Screenshots(f,()=>now);
        await s.TakeAsync(new(1,2),()=>true);s.OpenCrop();now=now.AddSeconds(10);s.Expire();Assert.True(s.IsCropping);
        s.CancelCrop();Assert.Single(f.Copies);now=now.AddSeconds(6);s.Expire();Assert.Null(s.Overlay);
    }
    [Fact] public async Task StaleWindowNeverCopies()
    {var f=new Fake();var s=new Screenshots(f);await s.TakeAsync(new(1,2),()=>false);Assert.Empty(f.Copies);Assert.Null(s.Overlay);}
    [Fact] public async Task FailedClipboardReportsAndNoPreview()
    {var f=new Fake{CopyOk=false};var s=new Screenshots(f);await s.TakeAsync(new(1,2),()=>true);Assert.Equal(ShotNotice.CopyFailed,s.Overlay!.Notice);}
    sealed class Pending : IScreenshotService {
        public List<TaskCompletionSource<CapturedImage?>> Requests=[];
        public List<CapturedImage> Copies=[];
        public Task<CapturedImage?> CaptureAsync(WindowKey key)
        {var t=new TaskCompletionSource<CapturedImage?>();Requests.Add(t);return t.Task;}
        public CapturedImage? Crop(CapturedImage image,WindowBounds b)=>null;
        public bool Copy(CapturedImage image){Copies.Add(image);return true;}
    }
    [Fact] public async Task DismissInvalidatesInFlightCapture()
    {
        var f=new Pending();var s=new Screenshots(f);
        var request=s.TakeAsync(new(1,2),()=>true);
        s.Dismiss();f.Requests[0].SetResult(new([1],100,100));await request;
        Assert.Empty(f.Copies);Assert.Null(s.Overlay);
    }
    [Fact] public async Task LatestCaptureWinsEvenWhenEarlierFinishesLater()
    {
        var f=new Pending();var s=new Screenshots(f);
        var first=s.TakeAsync(new(1,2),()=>true);var second=s.TakeAsync(new(1,2),()=>true);
        f.Requests[1].SetResult(new([2],200,200));await second;
        f.Requests[0].SetResult(new([1],100,100));await first;
        Assert.Single(f.Copies);Assert.Equal(200,s.Overlay!.Image!.Width);
    }
    [Fact] public async Task CropFailureLeavesOriginalClipboard()
    {
        var f=new Fake();var s=new Screenshots(f);
        await s.TakeAsync(new(1,2),()=>true);s.OpenCrop();s.FinishCrop(new(0,0,0,0));
        Assert.Single(f.Copies);Assert.Equal(ShotNotice.CropFailed,s.Overlay!.Notice);
    }
    [Fact] public void FitAndPixels()
    {Assert.Equal(new CropRect(0,25,100,50),CropGeometry.Fit(200,100,new(0,0,100,100)));Assert.Equal(new WindowBounds(100,50,300,100),CropGeometry.Pixels(new(.1,.1,.3,.2),1000,500));}
    [Theory]
    [InlineData(CropHandle.Left)] [InlineData(CropHandle.Right)] [InlineData(CropHandle.Top)] [InlineData(CropHandle.Bottom)]
    [InlineData(CropHandle.TopLeft)] [InlineData(CropHandle.TopRight)] [InlineData(CropHandle.BottomLeft)] [InlineData(CropHandle.BottomRight)]
    public void HandlesRespectBounds(CropHandle h)
    {foreach(var d in new[]{-2d,2d}){var r=CropGeometry.Resize(new(.2,.2,.6,.6),h,d,d,.05,.05);Assert.True(r.X>=0&&r.Y>=0&&r.Right<=1.00001&&r.Bottom<=1.00001&&r.Width>=.04999&&r.Height>=.04999);}}
}