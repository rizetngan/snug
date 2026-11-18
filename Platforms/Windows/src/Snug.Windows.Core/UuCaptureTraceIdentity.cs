namespace Snug.Windows.Core;

/// <summary>Only the persistent Snug monitor owns these trace names.
/// Temporary acceptance probes and other ETW consumers are not ours to stop.</summary>
public static class UuCaptureTraceIdentity
{
    public const string SessionName = "Snug-UU-Monitor-Service";
    public const string LegacyPrefix = "Snug-UU-Monitor-";
    public const string ServiceMutex = @"Global\Snug-UU-Capture-Monitor-Service";

    public static bool IsLegacyMonitor(string name) =>
        name.StartsWith(LegacyPrefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(name[LegacyPrefix.Length..], "N", out _);
}