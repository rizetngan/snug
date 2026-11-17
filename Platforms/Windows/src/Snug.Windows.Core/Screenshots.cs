namespace Snug.Windows.Core;

public sealed record CapturedImage(byte[] Png, int Width, int Height);
public enum ShotKind { Preview, Cropping, Notice }
public enum ShotNotice { Captured, Cropped, WindowGone, Failed, CopyFailed, CropFailed }
public sealed record ScreenshotOverlay(ShotKind Kind, CapturedImage? Image, ShotNotice Notice = ShotNotice.Captured);
public interface IScreenshotService
{
    Task<CapturedImage?> CaptureAsync(WindowKey key);
    CapturedImage? Crop(CapturedImage image, WindowBounds pixels);
    bool Copy(CapturedImage image);
}

/// <summary>UI-thread state machine adapted from upstream Screenshots.swift.
/// Last valid request wins; nothing written to disk.</summary>
public sealed class Screenshots(IScreenshotService service, Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private long _serial;
    private DateTimeOffset? _expires;
    public ScreenshotOverlay? Overlay { get; private set; }
    public bool IsCropping => Overlay?.Kind == ShotKind.Cropping;
    public event Action? Changed;

    public async Task TakeAsync(WindowKey key, Func<bool> stillCurrent)
    {
        if (IsCropping) return;
        var mine = ++_serial;
        CapturedImage? image;
        try { image = await service.CaptureAsync(key); }
        catch { image = null; }
        if (mine != _serial || IsCropping || !stillCurrent()) return;
        if (image is null) { Set(new(ShotKind.Notice, null, ShotNotice.Failed)); return; }
        if (!Copy(image)) { Set(new(ShotKind.Notice, null, ShotNotice.CopyFailed)); return; }
        Set(new(ShotKind.Preview, image));
    }
    public void OpenCrop()
    {
        if (Overlay is { Kind: ShotKind.Preview, Image: { } image }) { ++_serial; Set(new(ShotKind.Cropping,image)); }
    }
    public void CancelCrop()
    {
        if (Overlay is { Kind: ShotKind.Cropping, Image: { } image }) Set(new(ShotKind.Preview,image));
    }
    public void FinishCrop(CropRect rect)
    {
        if (Overlay is not { Kind: ShotKind.Cropping, Image: { } image }) return;
        CapturedImage? cropped;
        try { cropped = service.Crop(image,CropGeometry.Pixels(rect,image.Width,image.Height)); }
        catch { cropped = null; }
        Set(new(ShotKind.Notice,null,cropped is null ? ShotNotice.CropFailed :
            Copy(cropped) ? ShotNotice.Cropped : ShotNotice.CopyFailed));
    }
    private bool Copy(CapturedImage image)
    {
        try { return service.Copy(image); } catch { return false; }
    }
    public void Expire() { if (_expires is { } deadline && _clock() >= deadline) Set(null); }
    public void Dismiss() { ++_serial; Set(null); }
    private void Set(ScreenshotOverlay? overlay)
    {
        _expires = overlay?.Kind switch { ShotKind.Preview => _clock().AddSeconds(5), ShotKind.Notice => _clock().AddSeconds(3), _ => null };
        if (Overlay == overlay) return;
        Overlay = overlay; Changed?.Invoke();
    }
}