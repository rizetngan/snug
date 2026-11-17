using System.Diagnostics;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>Force termination is restricted to an observed user app, never a
/// process tree. Read identity from the actual Process, not its display name.</summary>
internal static class QuitProcess
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "Snug.Windows", "SnugLauncher", "GameViewer", "GameViewerServer",
        "GameViewerService", "System", "Idle", "csrss", "wininit", "winlogon", "services",
        "lsass", "smss", "svchost", "dwm", "fontdrvhost", "lsaiso", "conhost",
        "ShellExperienceHost", "StartMenuExperienceHost", "SearchApp", "TextInputHost", "RuntimeBroker"
    };

    public static QuitProcessIdentity? Identity(Process process)
    {
        try
        {
            if (process.Id == Environment.ProcessId || process.HasExited ||
                ProtectedNames.Contains(process.ProcessName)) return null;
            using var current = Process.GetCurrentProcess();
            if (process.SessionId != current.SessionId || process.SessionId == 0) return null;
            var path = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path)) return null;
            return new(process.Id, process.StartTime.ToUniversalTime().Ticks, path);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException) { return null; }
    }

    public static QuitProcessIdentity? Observe(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return Identity(process); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception) { return null; }
    }

    public static bool TryForce(QuitStalledNotice notice)
    {
        try
        {
            using var process = Process.GetProcessById(notice.Identity.ProcessId);
            // Keep a process handle open so an exit/recycled PID cannot target a different process.
            _ = process.Handle;
            if (!notice.CanForce(Identity(process), DateTimeOffset.UtcNow)) return false;
            process.Kill(entireProcessTree: false);
            return process.WaitForExit(3000);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException) { return false; }
    }
}