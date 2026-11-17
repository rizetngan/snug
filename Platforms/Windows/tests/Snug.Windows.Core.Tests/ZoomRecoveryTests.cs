using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class ZoomRecoveryTests
{
    private const string Code = @"C:\Apps\Code.exe";
    private const string Cursor = @"C:\Apps\Cursor.exe";
    private static WindowRestoreRecord Record(string path = Code, string title = "project - Visual Studio Code",
        int zoom = 2, long? start = 111) =>
        new(1, new WindowKey(100, 123), "Editor", path,
            new WindowPlacementData(0, 1, 0, 0, 0, 0, new(10, 10, 800, 600)),
            zoom, DateTimeOffset.UtcNow, title, new WindowBounds(1920, 0, 720, 1186), start);
    private static WindowInfo Returning(string path = Code, string title = "project - Visual Studio Code",
        uint pid = 200, long handle = 456) => new(new WindowKey(pid, handle), "Editor", title, path, false, false);

    [Theory]
    [InlineData(@"C:\Apps\Code.exe", ZoomScope.Workspace)]
    [InlineData(@"C:\Apps\Cursor.exe", ZoomScope.Workspace)]
    [InlineData(@"C:\Apps\Zed.exe", ZoomScope.Process)]
    [InlineData(@"C:\Apps\WindowsTerminal.exe", ZoomScope.Unverified)]
    [InlineData(@"C:\Apps\chrome.exe", ZoomScope.Unverified)]
    public void ClassifyByKnownExecutable(string path, ZoomScope expected) =>
        Assert.Equal(expected, ZoomRecovery.ScopeOf(path));

    [Fact]
    public void WorkspaceWithExactUniqueTitleAndMatchingAppCanCarryZoomAcrossProcess()
    {
        Assert.Equal(ZoomRecoveryAction.Repay, ZoomRecovery.Decide(Record(), Returning(), 222, true));
        Assert.Equal(ZoomRecoveryAction.Repay, ZoomRecovery.Decide(
            Record(Cursor, "project - Cursor"), Returning(Cursor, "project - Cursor"), 222, true));
    }

    [Fact]
    public void WorkspaceWithAmbiguousTitleOrNewWorkspaceDoesNotTouchZoom()
    {
        Assert.Equal(ZoomRecoveryAction.KeepForReview, ZoomRecovery.Decide(Record(), Returning(), 222, false));
        Assert.Equal(ZoomRecoveryAction.KeepForReview, ZoomRecovery.Decide(Record(), Returning(title: "other - Visual Studio Code"), 222, true));
        Assert.Equal(ZoomRecoveryAction.KeepForReview, ZoomRecovery.Decide(
            Record(Cursor, "Cursor"), Returning(Cursor, "Cursor"), 222, true));
    }

    [Fact]
    public void SamePidDoesNotProveSameProcessForUnverifiedApps()
    {
        var r = Record(@"C:\Apps\claude.exe", "Claude window");
        var w = Returning(@"C:\Apps\claude.exe", "Claude window", 100);
        Assert.Equal(ZoomRecoveryAction.Repay, ZoomRecovery.Decide(r, w, 111, true));
        Assert.Equal(ZoomRecoveryAction.KeepForReview, ZoomRecovery.Decide(r, w, 222, true));
        Assert.Equal(ZoomRecoveryAction.KeepForReview, ZoomRecovery.Decide(r with { ProcessStartedUtcTicks = null }, w, 111, true));
    }

    [Fact]
    public void ProcessWideStateOnlyExpiresIfOriginalProcessReallyExited()
    {
        var r = Record(@"C:\Apps\Zed.exe", "Zed workspace");
        var w = Returning(@"C:\Apps\Zed.exe", "Zed workspace");
        Assert.Equal(ZoomRecoveryAction.KeepForReview, ZoomRecovery.Decide(r, w, 222, true));
        Assert.Equal(ZoomRecoveryAction.DiscardProcessDebt, ZoomRecovery.Decide(r, w, 222, true, true));
    }

    [Fact]
    public void WindowsWorkspaceRestoreAllowsSmallBorderRoundingOnlyForMatchingTitle()
    {
        var file = Path.Combine(Path.GetTempPath(), "SnugZoomTest", Guid.NewGuid().ToString("N"), "left.json");
        try
        {
            var book = new LeftBehindBook(file);
            var now = DateTimeOffset.UtcNow;
            book.Add(Record(), now);
            Assert.NotNull(book.Match(Returning(), new WindowBounds(1919, 0, 723, 1187), now));
            Assert.Null(book.Match(Returning(title: "other - Visual Studio Code"),
                new WindowBounds(1919, 0, 723, 1187), now));
            Assert.Null(book.Match(Returning(), new WindowBounds(1919, 0, 725, 1187), now));
        }
        finally
        {
            var parent = Path.GetDirectoryName(file)!;
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }
    [Fact]
    public void WrongExecutableAndZeroDebtBehaveIndependently()
    {
        Assert.Equal(ZoomRecoveryAction.KeepForReview, ZoomRecovery.Decide(Record(), Returning(@"D:\Other\Code.exe"), 222, true));
        Assert.Equal(ZoomRecoveryAction.NoDebt, ZoomRecovery.Decide(Record(zoom: 0), Returning(), 222, true));
    }
}