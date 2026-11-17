using System.Diagnostics;
using System.IO;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>
/// Reads scoped controller-table evidence and legacy log compatibility hints.
/// The optional ETW pilot can override sticky hints only with a fresh,
/// verified-generation, complete capture inventory.
/// </summary>
public sealed class UuSessionDetector : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly string DefaultLogDirectory = @"D:\GameViewer\log\server\log";

    private readonly object _gate = new();
    private readonly UuControllerReader _controllerReader = new();
    private readonly UuCapturePilotReader? _capturePilot = UuCapturePilotReader.FromEnvironment();
    private UuServerGeneration? _serverGeneration;
    private string _logSummary = "等待 UU 会话日志";
    private DateTimeOffset _lastPoll = DateTimeOffset.MinValue;
    private bool _polling;
    private UuSignals _cached = new(false, null, false);

    public string LocalControllerSummary { get; private set; } = "未读取";
    public string LastSignalSummary { get; private set; } = "等待 UU 会话日志";
    public int? ServerProcessId { get; private set; }

    public UuSignals Sample()
    {
        var local = _controllerReader.Sample();
        LocalControllerSummary = local.Status;
        lock (_gate)
        {
            if (!_polling && DateTimeOffset.UtcNow - _lastPoll >= PollInterval)
            {
                _polling = true;
                _ = Task.Run(Poll);
            }
            var current = _cached with { Controllers = local.Controllers };
            if (_capturePilot is null) return current;
            var generation = DateTimeOffset.UtcNow - _lastPoll <= UuCaptureFeed.Freshness ? _serverGeneration : null;
            var capture = _capturePilot.Sample(generation);
            LastSignalSummary = capture.Phase switch
            {
                UuCapturePhase.Active => "ETW 捕获中 · 设备与屏幕未知",
                UuCapturePhase.Rebuilding => "ETW 捕获重建中",
                UuCapturePhase.Inactive => "ETW 捕获已停止",
                _ => "ETW 未知 · " + _logSummary
            };
            return UuCaptureLifecycle.Combine(current, capture);
        }
    }

    private void Poll()
    {
        UuSignals signals;
        string summary;
        int? serverPid;
        UuServerGeneration? generation = null;

        try
        {
            var serverProcesses = Process.GetProcessesByName("GameViewerServer");
            try
            {
                var pids = serverProcesses.Select(process => (uint)process.Id).ToHashSet();
                serverPid = pids.Count == 0 ? null : (int)pids.OrderByDescending(value => value).First();
                if (_capturePilot is not null && serverProcesses.Length == 1)
                    generation = UuServerIdentity.ReadSingle();

                if (pids.Count == 0)
                {
                    signals = new UuSignals(false, null, false);
                    summary = "GameViewerServer 未运行";
                }
                else
                {
                    var logDirectory = ResolveLogDirectory(serverProcesses);
                    var connectionLocked = IsCurrentSessionLogLocked(
                        logDirectory, "connection_log_controlled_", pids);
                    var streamerLocked = IsCurrentSessionLogLocked(
                        logDirectory, "streamer_log_controlled_", pids);

                    // These handles may be held for the server's entire lifetime.
                    // Retain the compatibility hint, not a fabricated capture signal.
                    signals = new UuSignals(
                        OverlayVisible: false,
                        Controller: null,
                        CaptureActive: null,
                        LogFallback: connectionLocked || streamerLocked);

                    summary = $"Server PID {serverPid} · connection {(connectionLocked ? "locked" : "idle")} · stream {(streamerLocked ? "locked" : "idle")} · 推流屏幕未知";
                }
            }
            finally
            {
                foreach (var process in serverProcesses) process.Dispose();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            signals = new UuSignals(false, null, null);
            summary = "UU 会话日志暂不可读：" + error.GetType().Name;
            serverPid = null;
            generation = null;
        }

        lock (_gate)
        {
            _cached = signals;
            _lastPoll = DateTimeOffset.UtcNow;
            _polling = false;
            _logSummary = summary;
            _serverGeneration = generation;
            if (_capturePilot is null) LastSignalSummary = summary;
            ServerProcessId = serverPid;
        }
    }

    private static bool IsCurrentSessionLogLocked(
        string directory,
        string prefix,
        IReadOnlySet<uint> serverPids)
    {
        if (!Directory.Exists(directory)) return false;

        foreach (var path in Directory.EnumerateFiles(directory, prefix + "*.slog"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var separator = name.LastIndexOf('_');
            if (separator < 0
                || !uint.TryParse(name[(separator + 1)..], out var pid)
                || !serverPids.Contains(pid))
                continue;

            if (IsFileHeldByServer(path)) return true;
        }

        return false;
    }

    private static bool IsFileHeldByServer(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 32 or 33)
        {
            // Only a sharing/lock violation is evidence; other I/O failures are not sessions.
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ResolveLogDirectory(IEnumerable<Process> serverProcesses)
    {
        foreach (var process in serverProcesses)
        {
            try
            {
                var executable = process.MainModule?.FileName;
                var binDirectory = Path.GetDirectoryName(executable);
                var appRoot = binDirectory is null ? null : Directory.GetParent(binDirectory)?.FullName;
                if (appRoot is null) continue;

                var candidate = Path.Combine(appRoot, "log", "server", "log");
                if (Directory.Exists(candidate)) return candidate;
            }
            catch (System.ComponentModel.Win32Exception) { }
            catch (InvalidOperationException) { }
        }

        return DefaultLogDirectory;
    }
    public void Dispose() => _capturePilot?.Dispose();
}
