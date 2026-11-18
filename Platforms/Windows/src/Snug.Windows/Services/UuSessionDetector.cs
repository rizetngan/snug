using System.Diagnostics;
using System.IO;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>
/// Reads scoped controller-table evidence and bounded streamer-log activity hints.
/// The SYSTEM startup monitor overrides sticky hints only with a fresh,
/// verified-generation, complete capture inventory.
/// </summary>
public sealed class UuSessionDetector : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly string DefaultLogDirectory = @"D:\GameViewer\log\server\log";

    private readonly object _gate = new();
    private readonly UuControllerReader _controllerReader = new();
    private readonly UuCaptureMonitorReader? _captureMonitor = UuCaptureMonitorReader.FromEnvironment();
    private readonly UuStreamLogActivityTracker _streamLogActivity = new();
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
            if (_captureMonitor is null) return current;
            var generation = DateTimeOffset.UtcNow - _lastPoll <= UuCaptureFeed.Freshness ? _serverGeneration : null;
            var capture = _captureMonitor.Sample(generation);
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
                if (_captureMonitor is not null && serverProcesses.Length == 1)
                    generation = UuServerIdentity.ReadSingle();

                if (pids.Count == 0)
                {
                    _streamLogActivity.Reset();
                    signals = new UuSignals(false, null, false);
                    summary = "GameViewerServer 未运行";
                }
                else if (serverProcesses.Length != 1 || serverPid is null)
                {
                    _streamLogActivity.Reset();
                    signals = new UuSignals(false, null, null);
                    summary = "GameViewerServer 多进程 · 推流状态未知";
                }
                else
                {
                    var logDirectory = ResolveLogDirectory(serverProcesses);
                    var streamPath = ResolveCurrentStreamerLog(logDirectory, serverPid.Value);
                    var streamActive = false;
                    var streamStatus = streamPath is null ? "missing" : "idle";
                    if (streamPath is not null)
                    {
                        try
                        {
                            var streamFile = new FileInfo(streamPath);
                            streamFile.Refresh();
                            streamActive = _streamLogActivity.Observe(
                                streamPath, streamFile.Length, DateTimeOffset.UtcNow);
                            if (streamActive) streamStatus = "growing";
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                        {
                            _streamLogActivity.Reset();
                            streamStatus = "unreadable";
                        }
                    }
                    else
                    {
                        _streamLogActivity.Reset();
                    }

                    signals = new UuSignals(
                        OverlayVisible: false,
                        Controller: null,
                        CaptureActive: null,
                        LogFallback: streamActive);

                    summary = "Server PID " + serverPid + " · streamer log " + streamStatus;
                }
            }
            finally
            {
                foreach (var process in serverProcesses) process.Dispose();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _streamLogActivity.Reset();
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
            if (_captureMonitor is null) LastSignalSummary = summary;
            ServerProcessId = serverPid;
        }
    }

    private static string? ResolveCurrentStreamerLog(string directory, int serverPid)
    {
        if (!Directory.Exists(directory)) return null;
        var pattern = "streamer_log_controlled_*_" + serverPid + ".slog";
        return Directory.EnumerateFiles(directory, pattern)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
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
    public void Dispose() => _captureMonitor?.Dispose();
}
