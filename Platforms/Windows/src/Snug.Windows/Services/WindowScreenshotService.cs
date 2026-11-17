using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Snug.Windows.Core;
using Snug.Windows.Interop;
using Clipboard = System.Windows.Clipboard;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Snug.Windows.Services;

/// <summary>Windows window-only capture adapter. The target HWND must still
/// belong to the recorded process. Capture uses native pixels, never UU frames
/// or the whole desktop. PrintWindow may fail on protected or GPU-only windows.</summary>
public sealed class WindowScreenshotService : IScreenshotService
{
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    private readonly SemaphoreSlim _captureGate = new(1,1);
    public async Task<CapturedImage?> CaptureAsync(WindowKey key)
    {
        if (!_captureGate.Wait(0)) return null;
        var task = Task.Run(() => {
            try { return Capture(key); }
            finally { _captureGate.Release(); }
        });
        // A stuck target window must not freeze UI or accumulate worker threads.
        return await Task.WhenAny(task,Task.Delay(2500)) == task ? await task : null;
    }

    private static CapturedImage? Capture(WindowKey key)
    {
        try
        {
            using var dpi = new PhysicalDpiScope();
            var h = new IntPtr(key.Handle);
            var wm = new WindowManager();
            if(!dpi.IsValid || !wm.IsResponsive(key) ||
                !NativeMethods.GetWindowRect(h,out var r)) return null;
            var w=r.Right-r.Left;var height=r.Bottom-r.Top;
            if(w<=0||height<=0||(long)w*height>32_000_000) return null;
            using var bmp=new System.Drawing.Bitmap(w,height,PixelFormat.Format24bppRgb);
            using(var graphics=System.Drawing.Graphics.FromImage(bmp))
            {
                var dc=graphics.GetHdc();
                try { if(!PrintWindow(h,dc,2))return null; }
                finally { graphics.ReleaseHdc(dc); }
            }
            if(!wm.IsAlive(key))return null;
            using var stream=new MemoryStream();
            bmp.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
            return new(stream.ToArray(),w,height);
        }
        catch { return null; }
    }

    public static BitmapSource Decode(CapturedImage image)
    {
        using var stream=new MemoryStream(image.Png,false);
        var decoder=new PngBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        var bitmap=decoder.Frames[0];bitmap.Freeze();return bitmap;
    }
    public CapturedImage? Crop(CapturedImage image,WindowBounds pixels)
    {
        if(pixels.Width<=0||pixels.Height<=0)return null;
        var result=new CroppedBitmap(Decode(image),new System.Windows.Int32Rect(pixels.X,pixels.Y,pixels.Width,pixels.Height));
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(result));
        using var stream=new MemoryStream();encoder.Save(stream);
        return new(stream.ToArray(),pixels.Width,pixels.Height);
    }
    public bool Copy(CapturedImage image)
    {
        try {
            // Keep PNG and the Windows bitmap interchange format for other apps.
            var data=new DataObject();
            data.SetImage(Decode(image));
            data.SetData("PNG",new MemoryStream(image.Png,false));
            Clipboard.SetDataObject(data,true);
            return true;
        } catch (ExternalException) { return false; }
    }
}