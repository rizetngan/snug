using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class NewWindowMenuTests
{
    private static NewWindowMenu.Item M(string name, uint id,
        string? parent = null, bool enabled = true, bool submenu = false) =>
        new(name, id, parent, enabled, submenu);

    [Fact]
    public void RealNewWindowIsPreferredOverGenericNewDocument()
    {
        var selected = NewWindowMenu.Pick([
            M("New\tCtrl+N", 101),
            M("New Window\tCtrl+Shift+N", 102),
            M("New File\tCtrl+N", 103)
        ]);
        Assert.Equal(102u, selected?.CommandId);
    }

    [Theory]
    [InlineData("New Window")]
    [InlineData("New Window...")]
    [InlineData("&New Window\tCtrl+N")]
    [InlineData("新建窗口")]
    [InlineData("新建窗口…")]
    [InlineData("建立新視窗")]
    [InlineData("新增視窗")]
    [InlineData("open new window")]
    public void ExactWindowCommandsAreAccepted(string title) =>
        Assert.Equal(7u, NewWindowMenu.Pick([M(title, 7)])?.CommandId);

    [Fact]
    public void NativeSubmenuWindowChoiceIsAccepted()
    {
        Assert.Equal(12u, NewWindowMenu.Pick([
            M("New Window", 0, submenu: true),
            M("Default Profile", 12, "New Window")
        ])?.CommandId);
    }

    [Theory]
    [InlineData("New")]
    [InlineData("New File")]
    [InlineData("New Document")]
    [InlineData("New Project")]
    [InlineData("New Tab")]
    [InlineData("新建文件")]
    [InlineData("新建文档")]
    public void DocumentAndTabCommandsAreNeverMisclassified(string title) =>
        Assert.Null(NewWindowMenu.Pick([M(title, 9)]));

    [Fact]
    public void DisabledAndInvalidCommandsAreNotInvoked()
    {
        Assert.Equal(12u, NewWindowMenu.Pick([
            M("New Window", 8, enabled: false),
            M("New Window", 0),
            M("New Window", 99999),
            M("New Window", 12)
        ])?.CommandId);
    }

    [Fact]
    public void AWindowMenuCommandWithShortcutCanBeUsedWhenNoNewWindowLabelExists() =>
        Assert.Equal(18u, NewWindowMenu.Pick([M("Open Editor Window\tCtrl+N", 18)])?.CommandId);

    [Theory]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", NewWindowMenu.Shortcut.CtrlN)]
    [InlineData(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", NewWindowMenu.Shortcut.CtrlN)]
    [InlineData(@"C:\Program Files\Mozilla Firefox\firefox.exe", NewWindowMenu.Shortcut.CtrlN)]
    [InlineData(@"C:\Users\x\AppData\Local\Programs\cursor\Cursor.exe", NewWindowMenu.Shortcut.CtrlShiftN)]
    [InlineData(@"C:\Program Files\Microsoft VS Code\Code.exe", NewWindowMenu.Shortcut.CtrlShiftN)]
    [InlineData(@"C:\Other\notepad.exe", NewWindowMenu.Shortcut.None)]
    [InlineData(@"C:\Apps\FictionEditor.exe", NewWindowMenu.Shortcut.None)]
    public void KnownShortcutsAreExplicitlyAllowlisted(string path, NewWindowMenu.Shortcut expected)
    {
        var app = new AppInfo(path, "App", ExecutablePath: path);
        Assert.Equal(expected, NewWindowMenu.KnownShortcut(app));
    }

    [Fact]
    public void MenuBranchDetectionMatchesWindowWordsButNotFile()
    {
        Assert.True(NewWindowMenu.WorthOpening("Open Window"));
        Assert.True(NewWindowMenu.WorthOpening("新建窗口"));
        Assert.False(NewWindowMenu.WorthOpening("New File"));
    }
}
