using System.Globalization;
using Pinyin.NET;

namespace Snug.Windows.Core;

/// <summary>App 的稳定身份。Windows 桌面 App 以规范化可执行路径识别，打包 App 使用 AUMID。</summary>
public sealed record AppInfo(
    string AppId,
    string Name,
    string? ExecutablePath = null,
    string? LaunchTarget = null,
    string? EnglishName = null,
    string? IconPath = null,
    string? LaunchArguments = null);

/// <summary>本机安装的 App，以及用于排序和搜索的预计算拼写。</summary>
public sealed record InstalledApp
{
    public AppInfo App { get; }
    public SearchKeys Keys { get; }

    public InstalledApp(AppInfo app, string? englishName = null)
    {
        App = app;
        Keys = new SearchKeys(app.Name, englishName ?? app.EnglishName ?? app.Name);
    }

    public string Id => App.AppId;
}

/// <summary>一个运行中的 App 进程及其窗口。没有窗口的进程也保留在列表里。</summary>
public sealed record RunningApp(int ProcessId, AppInfo App, IReadOnlyList<WindowInfo> Windows)
{
    public string Id => App.AppId;

    public string WindowSummary => Windows.Count switch
    {
        0 => "正在运行",
        1 => string.IsNullOrWhiteSpace(Windows[0].Title) ? "1 个窗口" : Windows[0].Title,
        _ => $"{Windows.Count} 个窗口"
    };
}

/// <summary>显示名、英文名、拼音全拼和首字母；只在 App 目录刷新时计算一次。</summary>
public sealed class SearchKeys
{
    private const int MaxPronunciationVariants = 64;
    private readonly string[] _words;
    private readonly string[] _initials;

    public string SortKey { get; }
    public string Initials => _initials.FirstOrDefault() ?? string.Empty;

    public SearchKeys(string name, string english)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var processor = new PinyinProcessor();
        var tokens = processor.GetTokens(name);
        var choices = tokens
            .Select(token =>
            {
                var values = token.Full
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return values.Length == 0 ? [token.Original.ToLowerInvariant()] : values;
            })
            .ToArray();

        if (choices.Length == 0)
            choices = [[Normalize(name)]];

        SortKey = string.Join(" ", choices.Select(options => options[0]));
        var pinyin = Combine(choices);
        var initialChoices = choices.Select(options => options
            .Select(value => value.Length == 0 ? string.Empty : value[..1])
            .Distinct(StringComparer.Ordinal)
            .ToArray()).ToArray();
        _initials = Combine(initialChoices);

        _words = new[] { name, english }
            .Concat(pinyin)
            .Select(Normalize)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>开头就匹配（含首字母）得 2 分，仅包含得 1 分。</summary>
    public int Score(string query)
    {
        var normalized = Normalize(query);
        if (normalized.Length == 0) return 0;
        if (_words.Any(word => word.StartsWith(normalized, StringComparison.Ordinal))
            || _initials.Any(initial => initial.StartsWith(normalized, StringComparison.Ordinal)))
            return 2;
        return _words.Any(word => word.Contains(normalized, StringComparison.Ordinal)) ? 1 : 0;
    }

    public static string Normalize(string value) =>
        string.Concat((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character)))
            .ToLowerInvariant();

    private static string[] Combine(string[][] choices)
    {
        var values = new List<string> { string.Empty };
        foreach (var options in choices)
        {
            var next = new List<string>(Math.Min(MaxPronunciationVariants, values.Count * options.Length));
            foreach (var prefix in values)
            {
                foreach (var option in options)
                {
                    next.Add(prefix + option);
                    if (next.Count >= MaxPronunciationVariants) break;
                }
                if (next.Count >= MaxPronunciationVariants) break;
            }
            values = next;
        }
        return values.ToArray();
    }
}

public sealed record AppSearchResult(
    IReadOnlyList<AppInfo> Apps,
    IReadOnlyList<WindowInfo> Windows)
{
    public bool IsEmpty => Apps.Count == 0 && Windows.Count == 0;
}

public sealed record AppLetterGroup(string Letter, IReadOnlyList<InstalledApp> Apps);

public enum AppActionKind { PullWindow, OpenApp }

public sealed record AppAction(AppActionKind Kind, AppInfo App, WindowInfo? Window = null);

/// <summary>原版 AppList 的纯逻辑移植：排序、稳定刷新、分组、搜索和点击行为。</summary>
public static class AppList
{
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Snug.Windows", "SnugLauncher", "GameViewer", "GameViewerServer", "GameViewerService"
    };

    private static readonly HashSet<string> UnquittableExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe"
    };

    public static bool CanQuit(AppInfo app)
    {
        var executable = Path.GetFileName(app.ExecutablePath ?? app.LaunchTarget ?? app.AppId);
        return !UnquittableExecutables.Contains(executable);
    }

    public static bool IsExcluded(AppInfo app)
    {
        var executable = Path.GetFileNameWithoutExtension(app.ExecutablePath ?? app.LaunchTarget ?? app.AppId);
        return ExcludedProcessNames.Contains(executable);
    }

    /// <summary>运行中的 App 按最前窗口排序；同一 App 的窗口也按前后层次排序。</summary>
    public static IReadOnlyList<RunningApp> Ordered(
        IEnumerable<RunningApp> apps,
        IReadOnlyList<long> frontToBack)
    {
        var rank = frontToBack
            .Select((handle, index) => (handle, index))
            .GroupBy(item => item.handle)
            .ToDictionary(group => group.Key, group => group.First().index);

        int Position(WindowInfo window) => rank.TryGetValue(window.Key.Handle, out var index) ? index : int.MaxValue;

        var ordered = apps.Where(app => !IsExcluded(app.App)).Select(app =>
        {
            var windows = StableSort(app.Windows, Position);
            return app with { Windows = windows };
        });
        return StableSort(ordered, app => app.Windows.Count == 0 ? int.MaxValue : Position(app.Windows[0]));
    }

    /// <summary>列表显示期间不重排：保留旧位置，只更新内容、移除已消失项并把新项接在末尾。</summary>
    public static IReadOnlyList<RunningApp> KeepingOrder(
        IReadOnlyList<RunningApp> shown,
        IReadOnlyList<RunningApp> fresh)
    {
        var latest = fresh.GroupBy(app => app.ProcessId).ToDictionary(group => group.Key, group => group.First());
        var known = shown.Select(app => app.ProcessId).ToHashSet();
        var kept = new List<RunningApp>();

        foreach (var old in shown)
        {
            if (!latest.TryGetValue(old.ProcessId, out var current)) continue;
            kept.Add(current with { Windows = KeepingOrder(old.Windows, current.Windows) });
        }

        kept.AddRange(fresh.Where(app => !known.Contains(app.ProcessId)));
        return kept;
    }

    public static IReadOnlyList<WindowInfo> KeepingOrder(
        IReadOnlyList<WindowInfo> shown,
        IReadOnlyList<WindowInfo> fresh)
    {
        var latest = fresh.GroupBy(window => window.Key).ToDictionary(group => group.Key, group => group.First());
        var known = shown.Select(window => window.Key).ToHashSet();
        return shown.Where(window => latest.ContainsKey(window.Key))
            .Select(window => latest[window.Key])
            .Concat(fresh.Where(window => !known.Contains(window.Key)))
            .ToArray();
    }

    /// <summary>按拼音字典序排序；同一 App 的重复快捷方式只保留第一份。</summary>
    public static IReadOnlyList<InstalledApp> Sorted(IEnumerable<InstalledApp> apps)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return apps
            .Where(item => !IsExcluded(item.App) && seen.Add(item.App.AppId))
            .OrderBy(item => item.Keys.SortKey, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<AppLetterGroup> Grouped(IReadOnlyList<InstalledApp> apps)
    {
        return apps
            .GroupBy(item =>
            {
                var first = item.Keys.SortKey.FirstOrDefault();
                return first is >= 'a' and <= 'z' ? char.ToUpperInvariant(first).ToString() : "#";
            })
            .OrderBy(group => group.Key == "#" ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new AppLetterGroup(group.Key, group.ToArray()))
            .ToArray();
    }

    /// <summary>搜索 App 中文名、英文名、拼音全拼、首字母，以及运行中窗口标题。</summary>
    public static AppSearchResult Search(
        string query,
        IReadOnlyList<InstalledApp> installed,
        IReadOnlyList<RunningApp> running)
    {
        var normalized = SearchKeys.Normalize(query);
        if (normalized.Length == 0) return new AppSearchResult([], []);

        var installedIds = installed.Select(item => item.App.AppId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = installed.Concat(running
            .Where(app => !installedIds.Contains(app.App.AppId))
            .Select(app => new InstalledApp(app.App)));

        var appResults = candidates
            .Select((item, index) => (item, index, score: item.Keys.Score(query)))
            .Where(item => item.score > 0)
            .OrderByDescending(item => item.score)
            .ThenBy(item => item.index)
            .Select(item => item.item.App)
            .ToArray();

        var windows = running.SelectMany(app => app.Windows)
            .Where(window => SearchKeys.Normalize(window.Title).Contains(normalized, StringComparison.Ordinal))
            .ToArray();

        return new AppSearchResult(appResults, windows);
    }

    /// <summary>点 App 与点 Dock 图标一致：已有窗口就打开最近使用的窗口，否则启动/唤回 App。</summary>
    public static AppAction ActionFor(AppInfo app, IReadOnlyList<RunningApp> running)
    {
        var current = running.FirstOrDefault(item =>
            string.Equals(item.App.AppId, app.AppId, StringComparison.OrdinalIgnoreCase));
        var window = current?.Windows.FirstOrDefault();
        return window is null
            ? new AppAction(AppActionKind.OpenApp, app)
            : new AppAction(AppActionKind.PullWindow, app, window);
    }

    private static IReadOnlyList<T> StableSort<T>(IEnumerable<T> source, Func<T, int> key) =>
        source.Select((item, index) => (item, index))
            .OrderBy(entry => key(entry.item))
            .ThenBy(entry => entry.index)
            .Select(entry => entry.item)
            .ToArray();
}
