using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class ZoomBookTests
{
    [Fact]
    public void RemembersZoomPerApplication()
    {
        var book = new ZoomBook();
        const string cursor = @"C:\Program Files\Cursor\Cursor.exe";
        const string terminal = @"C:\Program Files\WindowsApps\WindowsTerminal.exe";

        book.Remember(cursor, 4);
        book.Remember(terminal, 1);

        Assert.Equal(4, book.StepsFor(cursor, 2));
        Assert.Equal(1, book.StepsFor(terminal, 2));
        Assert.Equal(2, book.StepsFor(@"C:\Program Files\Other\Other.exe", 2));
    }

    [Fact]
    public void RestoresRememberedValuesFromSettings()
    {
        var persisted = new Dictionary<string, int>
        {
            [@"C:\Program Files\Microsoft VS Code\Code.exe"] = 3
        };

        var book = new ZoomBook(persisted);

        Assert.Equal(3, book.StepsFor(@"C:\Program Files\Microsoft VS Code\Code.exe", 1));
    }

    [Fact]
    public void UsesApplicationNameWhenExecutablePathIsUnavailable()
    {
        var book = new ZoomBook();
        Assert.True(ZoomBook.IsZoomable(string.Empty, "Cursor"));
        book.Remember(string.Empty, "Cursor", 3);
        var restored = new ZoomBook(book.Remembered);
        Assert.Equal(3, restored.StepsFor(string.Empty, "Cursor", 1));
    }

    [Fact]
    public void ClampsRememberedValuesToSupportedRange()
    {
        var book = new ZoomBook(new Dictionary<string, int>
        {
            [@"C:\Program Files\Cursor\Cursor.exe"] = 99
        });

        Assert.Equal(ZoomBook.MaximumSteps,
            book.StepsFor(@"C:\Program Files\Cursor\Cursor.exe", 0));
    }
}
