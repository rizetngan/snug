namespace Snug.Windows.Core;

public enum ZoomScope { Workspace, Process, Unverified }
public enum ZoomRecoveryAction { NoDebt, Repay, DiscardProcessDebt, KeepForReview }

/// <summary>Adapt upstream ZoomBook scopes to Windows. A returning window
/// needs both the independently matched filled geometry and stronger zoom identity.
/// Do not silently erase debt when a workspace cannot be identified.</summary>
public static class ZoomRecovery
{
    public static ZoomScope ScopeOf(string executablePath)
    {
        var name = Path.GetFileName(executablePath);
        if (name.Equals("Code.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Cursor.exe", StringComparison.OrdinalIgnoreCase))
            return ZoomScope.Workspace;
        if (name.Equals("Zed.exe", StringComparison.OrdinalIgnoreCase))
            return ZoomScope.Process;
        return ZoomScope.Unverified;
    }

    public static ZoomRecoveryAction Decide(WindowRestoreRecord record, WindowInfo window,
        long? processStartedUtcTicks, bool uniqueTitle, bool originalProcessExited = false)
    {
        if (record.ZoomSteps == 0) return ZoomRecoveryAction.NoDebt;
        if (!StringComparer.OrdinalIgnoreCase.Equals(record.ExecutablePath, window.ExecutablePath))
            return ZoomRecoveryAction.KeepForReview;
        var sameTitle = !string.IsNullOrWhiteSpace(record.Title)
            && StringComparer.Ordinal.Equals(record.Title, window.Title);
        var sameProcess = record.Key.ProcessId == window.Key.ProcessId
            && record.ProcessStartedUtcTicks.HasValue && processStartedUtcTicks.HasValue
            && record.ProcessStartedUtcTicks == processStartedUtcTicks;
        var scope = ScopeOf(record.ExecutablePath);
        if (scope == ZoomScope.Process && originalProcessExited && record.ProcessStartedUtcTicks.HasValue
            && processStartedUtcTicks.HasValue && !sameProcess)
            return ZoomRecoveryAction.DiscardProcessDebt;
        if (!sameTitle || !uniqueTitle) return ZoomRecoveryAction.KeepForReview;
        if (sameProcess) return ZoomRecoveryAction.Repay;
        // Workspace titles are evidence only when they identify a workspace,
        // not a generic welcome window ("Cursor" / "Visual Studio Code").
        if (scope == ZoomScope.Workspace && IsWorkspaceTitle(window.Title))
            return ZoomRecoveryAction.Repay;
        return ZoomRecoveryAction.KeepForReview;
    }

    public static bool IsWorkspaceTitle(string title) =>
        !string.IsNullOrWhiteSpace(title) &&
        (title.Contains(" - Cursor", StringComparison.OrdinalIgnoreCase) ||
         title.Contains(" - Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
         title.Contains(" - Code - OSS", StringComparison.OrdinalIgnoreCase));
}