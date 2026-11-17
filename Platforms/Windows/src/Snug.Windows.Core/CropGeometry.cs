namespace Snug.Windows.Core;

// Upstream CropGeometry: stored selection is normalized and independent of view size.
public readonly record struct CropRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public static CropRect Full => new(0, 0, 1, 1);
}
public enum CropHandle { TopLeft, Top, TopRight, Left, Right, BottomLeft, Bottom, BottomRight }
public static class CropGeometry
{
    public static CropRect Fit(double width, double height, CropRect area)
    {
        if (width <= 0 || height <= 0 || area.Width <= 0 || area.Height <= 0) return default;
        var s = Math.Min(area.Width / width, area.Height / height);
        return new(area.X + (area.Width - width*s)/2, area.Y + (area.Height-height*s)/2, width*s, height*s);
    }
    public static WindowBounds Pixels(CropRect rect, int width, int height)
    {
        if (width <= 0 || height <= 0) return default;
        int x = (int)Math.Round(Math.Clamp(rect.X, 0, 1)*width);
        int y = (int)Math.Round(Math.Clamp(rect.Y, 0, 1)*height);
        int right = (int)Math.Round(Math.Clamp(rect.Right, 0, 1)*width);
        int bottom = (int)Math.Round(Math.Clamp(rect.Bottom, 0, 1)*height);
        return new(x,y,Math.Max(0,right-x),Math.Max(0,bottom-y));
    }
    public static CropRect Resize(CropRect rect, CropHandle handle, double dx, double dy, double minX, double minY)
    {
        var l=rect.X; var t=rect.Y; var r=rect.Right; var b=rect.Bottom;
        minX=Math.Clamp(minX,0,rect.Width); minY=Math.Clamp(minY,0,rect.Height);
        if (handle is CropHandle.Left or CropHandle.TopLeft or CropHandle.BottomLeft) l=Math.Clamp(l+dx,0,r-minX);
        if (handle is CropHandle.Right or CropHandle.TopRight or CropHandle.BottomRight) r=Math.Clamp(r+dx,l+minX,1);
        if (handle is CropHandle.Top or CropHandle.TopLeft or CropHandle.TopRight) t=Math.Clamp(t+dy,0,b-minY);
        if (handle is CropHandle.Bottom or CropHandle.BottomLeft or CropHandle.BottomRight) b=Math.Clamp(b+dy,t+minY,1);
        return new(l,t,r-l,b-t);
    }
}