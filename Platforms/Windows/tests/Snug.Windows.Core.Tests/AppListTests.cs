using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class AppListTests
{
    private static AppInfo App(string id, string name, string? english = null) =>
        new(id, name, EnglishName: english ?? name);

    private static WindowInfo Window(uint pid, long handle, string title = "") =>
        new(new WindowKey(pid, handle), $"App{pid}", title, string.Empty, false, false);

    private static RunningApp Running(uint pid, AppInfo app, params WindowInfo[] windows) =>
        new((int)pid, app, windows);

    [Fact]
    public void ChineseNamesSupportEnglishPinyinAndInitials()
    {
        var keys = new SearchKeys("备忘录", "Notes");

        Assert.Equal(2, keys.Score("备忘"));
        Assert.Equal(2, keys.Score("notes"));
        Assert.Equal(2, keys.Score("beiwanglu"));
        Assert.Equal(2, keys.Score("bwl"));
        Assert.Equal(0, keys.Score("calendar"));
    }

    [Fact]
    public void SortDeduplicatesAndGroupsByPinyinInitial()
    {
        var apps = AppList.Sorted([
            new InstalledApp(App("com.example.wechat", "微信", "WeChat")),
            new InstalledApp(App("com.example.bionic", "Bionic")),
            new InstalledApp(App("com.example.notes", "备忘录", "Notes")),
            new InstalledApp(App("com.example.wechat", "微信副本", "WeChat"))
        ]);

        Assert.Equal(["备忘录", "Bionic", "微信"], apps.Select(item => item.App.Name));
        var groups = AppList.Grouped(apps);
        Assert.Equal(["B", "W"], groups.Select(group => group.Letter));
        Assert.Equal(["备忘录", "Bionic"], groups[0].Apps.Select(item => item.App.Name));
    }

    [Fact]
    public void SearchFindsInstalledAndRunningOnlyAppsAndWindowTitles()
    {
        var installed = AppList.Sorted([
            new InstalledApp(App("wechat", "微信", "WeChat"))
        ]);
        var running = new[]
        {
            Running(10, App("wechat", "微信", "WeChat"), Window(10, 101, "聊天窗口")),
            Running(20, App("dev", "开发版"), Window(20, 201, "Snug issue tracker"))
        };

        var appResult = AppList.Search("wx", installed, running);
        Assert.Equal(["微信"], appResult.Apps.Select(app => app.Name));

        var runningOnly = AppList.Search("kaifa", installed, running);
        Assert.Equal(["开发版"], runningOnly.Apps.Select(app => app.Name));

        var windowResult = AppList.Search("snug", installed, running);
        Assert.Equal([201L], windowResult.Windows.Select(window => window.Key.Handle));
    }

    [Fact]
    public void RunningOrderFollowsFrontWindowAndStableRefreshKeepsVisiblePositions()
    {
        var first = Running(1, App("one", "One"), Window(1, 11), Window(1, 12));
        var second = Running(2, App("two", "Two"), Window(2, 21));
        var third = Running(3, App("three", "Three"));
        var ordered = AppList.Ordered([first, second, third], [21, 12, 11]);
        Assert.Equal([2, 1, 3], ordered.Select(app => app.ProcessId));

        var fresh = new[]
        {
            Running(2, App("two", "Two"), Window(2, 21)),
            Running(4, App("four", "Four")),
            Running(1, App("one", "One"), Window(1, 13), Window(1, 12, "新标题"))
        };
        var kept = AppList.KeepingOrder([first, second], fresh);
        Assert.Equal([1, 2, 4], kept.Select(app => app.ProcessId));
        Assert.Equal([12L, 13L], kept[0].Windows.Select(window => window.Key.Handle));
        Assert.Equal("新标题", kept[0].Windows[0].Title);
    }

    [Fact]
    public void AppTapPullsMostRecentWindowOrOpensWhenNoWindowExists()
    {
        var app = App("cursor", "Cursor");
        var running = Running(7, app, Window(7, 701), Window(7, 702));

        var pull = AppList.ActionFor(app, [running]);
        Assert.Equal(AppActionKind.PullWindow, pull.Kind);
        Assert.Equal(701L, pull.Window?.Key.Handle);

        var open = AppList.ActionFor(App("new", "New App"), [running]);
        Assert.Equal(AppActionKind.OpenApp, open.Kind);
        Assert.Null(open.Window);
    }

    [Fact]
    public void WindowsShellCannotBeQuitAndSnugInfrastructureIsExcluded()
    {
        Assert.False(AppList.CanQuit(new AppInfo("explorer.exe", "Windows Explorer", ExecutablePath: @"C:\Windows\explorer.exe")));
        Assert.True(AppList.CanQuit(App("cursor", "Cursor", "Cursor.exe")));
        Assert.True(AppList.IsExcluded(App("gameviewerserver", "UU Remote", "GameViewerServer.exe")));
        Assert.False(AppList.IsExcluded(App("cursor", "Cursor", "Cursor.exe")));
    }
}
