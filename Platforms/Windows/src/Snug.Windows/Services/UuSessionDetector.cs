using System.Diagnostics;
using System.IO;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>
/// 只读观察 UU Remote Server 的会话日志句柄。
/// controlled 日志由当前 GameViewerServer 进程持有时，代表这台电脑正处于被控会话。
/// 不轮询 uuyc-cli（它每次启动都会生成一份独立 slog），不修改 UU 配置。
/// </summary>
public sealed class UuSessionDetector
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly string DefaultLogDirectory = @"D:\GameViewer\log\server\log";

    private readonly object _gate = new();
    private DateTimeOffset _lastPoll = DateTimeOffset.MinValue;
    private bool _polling;
    private UuSignals _cached = new(false, null, false);

    public string LastSignalSummary { get; private set; } = "等待 UU 会话日志";
    public int? ServerProcessId { get; private set; }

    public UuSignals Sample()
    {
        lock (_gate)
        {
            if (!_polling && DateTimeOffset.UtcNow - _lastPoll >= PollInterval)
            {
                _polling = true;
                _ = Task.Run(Poll);
            }
            return _cached;
        }
    }

    private void Poll()
    {
        UuSignals signals;
        string summary;
        int? serverPid;

        try
        {
            var serverProcesses = Process.GetProcessesByName("GameViewerServer");
            try
            {
                var pids = serverProcesses.Select(process => (uint)process.Id).ToHashSet();
                serverPid = pids.Count == 0 ? null : (int)pids.OrderByDescending(value => value).First();

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

                    var inSession = connectionLocked || streamerLocked;
                    var controller = inSession
                        ? new ControllerInfo("UU Remote", ControllerKind.Unknown)
                        : null;

                    signals = new UuSignals(
                        OverlayVisible: false,
                        Controller: controller,
                        CaptureActive: streamerLocked);

                    summary = $"Server PID {serverPid} · connection {(connectionLocked ? "locked" : "idle")} · stream {(streamerLocked ? "locked" : "idle")}";
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
        }

        lock (_gate)
        {
            _cached = signals;
            _lastPoll = DateTimeOffset.UtcNow;
            _polling = false;
            LastSignalSummary = summary;
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
        catch (IOException)
        {
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
}
