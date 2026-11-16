using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class RestoreLedgerTests
{
    [Fact]
    public void SaveLoadAndClearRoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SnugTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "phone-screen.json");
        var ledger = new RestoreLedger(path);
        var record = new WindowRestoreRecord(
            1,
            new WindowKey(1234, 987654),
            "Cursor",
            "C:\\Tools\\Cursor.exe",
            new WindowPlacementData(0, 3, 0, 0, 0, 0, new WindowBounds(20, 30, 900, 700)),
            2,
            DateTimeOffset.Parse("2026-11-14T10:00:00Z"));

        try
        {
            Assert.Null(ledger.Load());
            ledger.Save(record);
            Assert.Equal(record, ledger.Load());
            Assert.True(File.Exists(path));

            ledger.Clear();

            Assert.False(File.Exists(path));
            Assert.Null(ledger.Load());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
