using System.Diagnostics;
using System.IO;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>统一维护安装 App 与当前运行 App；安装目录低频扫描，窗口状态按需刷新。</summary>
public sealed class AppCatalogService(WindowManager windows)
{
    private readonly InstalledAppScanner _scanner = new();
    private IReadOnlyList<InstalledApp> _installed = [];
    private IReadOnlyList<RunningApp> _running = [];

    public IReadOnlyList<InstalledApp> Installed => _installed;
    public IReadOnlyList<RunningApp> Running => _running;
    public bool IsLoaded { get; private set; }

    public async Task LoadInstalledAsync()
    {
        _installed = await _scanner.ScanAsync();
        IsLoaded = true;
    }

    public IReadOnlyList<RunningApp> RefreshRunning() => RefreshRunning(windows.ListWindows());

    public IReadOnlyList<RunningApp> RefreshRunning(IReadOnlyList<WindowInfo> visibleWindows)
    {
        var grouped = visibleWindows
            .GroupBy(window => window.Key.ProcessId)
            .Select(group =>
            {
                var app = ResolveApp(group.First());
                var appWindows = group.Select(window => window with { AppName = app.Name }).ToArray();
                return new RunningApp((int)group.Key, app, appWindows);
            })
            .ToArray();

        var ordered = AppList.Ordered(grouped, visibleWindows.Select(window => window.Key.Handle).ToArray());
        _running = _running.Count == 0 ? ordered : AppList.KeepingOrder(_running, ordered);
        return _running;
    }

    public AppInfo ResolveApp(WindowInfo window)
    {
        var path = Normalize(window.ExecutablePath);
        if (path.Length > 0)
        {
            var exact = _installed.FirstOrDefault(item =>
                string.Equals(Normalize(item.App.ExecutablePath), path, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact.App;
        }

        var processName = Path.GetFileNameWithoutExtension(window.ExecutablePath);
        if (processName.Length == 0) processName = window.AppName;
        var byName = _installed.FirstOrDefault(item =>
            string.Equals(Path.GetFileNameWithoutExtension(item.App.ExecutablePath), processName,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.App.Name, processName, StringComparison.OrdinalIgnoreCase));
        if (byName is not null) return byName.App;

        if (processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            var title = SearchKeys.Normalize(window.Title);
            if (title.Length >= 2)
            {
                var hosted = _installed
                    .Select(item => new
                    {
                        Item = item,
                        Names = new[] { item.App.Name, item.App.EnglishName ?? item.App.Name }
                            .Select(SearchKeys.Normalize).ToArray()
                    })
                    .Select(candidate => new
                    {
                        candidate.Item,
                        Score = candidate.Names.Max(name =>
                            name.Length < 2 ? 0
                            : string.Equals(name, title, StringComparison.Ordinal) ? 1000 + name.Length
                            : name.Length >= 3 && title.Contains(name, StringComparison.Ordinal) ? name.Length
                            : 0)
                    })
                    .Where(candidate => candidate.Score > 0)
                    .OrderByDescending(candidate => candidate.Score)
                    .Select(candidate => candidate.Item)
                    .FirstOrDefault();
                if (hosted is not null) return hosted.App;
            }
        }

        var displayName = ProductName(window.ExecutablePath) ?? window.AppName;
        var appId = path.Length > 0 ? "exe:" + path : "process:" + processName.ToLowerInvariant();
        return new AppInfo(appId, displayName, window.ExecutablePath,
            window.ExecutablePath, displayName, window.ExecutablePath);
    }

    public static Process? FindProcess(RunningApp app)
    {
        try { return Process.GetProcessById(app.ProcessId); }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
        catch (ArgumentException) { return path; }
        catch (NotSupportedException) { return path; }
    }

    private static string? ProductName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var executable = path;
        if (!File.Exists(executable)) return null;
        try
        {
            var name = FileVersionInfo.GetVersionInfo(executable).ProductName?.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
