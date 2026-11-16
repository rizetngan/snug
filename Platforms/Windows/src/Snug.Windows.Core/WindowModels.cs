namespace Snug.Windows.Core;

public readonly record struct WindowKey(uint ProcessId, long Handle);

public sealed record WindowInfo(
    WindowKey Key,
    string AppName,
    string Title,
    string ExecutablePath,
    bool IsMinimized,
    bool IsMaximized);

public readonly record struct WindowBounds(int X, int Y, int Width, int Height);

public enum WindowShowState { Normal, Maximized, Minimized }

public sealed record WindowPlacementData(
    int Flags,
    int ShowCommand,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    WindowBounds NormalPosition);

public sealed record WindowRestoreRecord(
    int SchemaVersion,
    WindowKey Key,
    string AppName,
    string ExecutablePath,
    WindowPlacementData OriginalPlacement,
    int ZoomSteps,
    DateTimeOffset CapturedAt,
    string? Title = null,
    WindowBounds? FilledBounds = null);

public sealed record DisplayInfo(
    string DeviceName,
    string FriendlyName,
    string DeviceId,
    WindowBounds Bounds,
    bool IsPrimary,
    bool IsVirtual,
    double DpiScaleX = 1.0,
    double DpiScaleY = 1.0);