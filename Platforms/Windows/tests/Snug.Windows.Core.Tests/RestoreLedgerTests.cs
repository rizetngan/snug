using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class RestoreLedgerTests
{
    [Fact]
    public void LegacyRestoreRecordWithoutTitleOrFilledBoundsStillLoads()
    {
        var path = Path.Combine(Path.GetTempPath(), "SnugTests",
            Guid.NewGuid().ToString("N"), "phone-screen.json");
        var record = new WindowRestoreRecord(1, new WindowKey(445, 12345),
            "Code", @"C:\Tools\Code.exe",
            new WindowPlacementData(0, 1, 0, 0, 0, 0,
                new WindowBounds(30, 40, 850, 650)),
            1, DateTimeOffset.Parse("2026-11-16T10:00:00Z"));

        try
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(
                System.Text.Json.JsonSerializer.Serialize(record))!.AsObject();
            json.Remove(nameof(WindowRestoreRecord.Title));
            json.Remove(nameof(WindowRestoreRecord.FilledBounds));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json.ToJsonString());

            var loaded = new RestoreLedger(path).Load();
            Assert.NotNull(loaded);
            Assert.Equal(record.Key, loaded.Key);
            Assert.Null(loaded.Title);
            Assert.Null(loaded.FilledBounds);
            Assert.Equal(record.OriginalPlacement, loaded.OriginalPlacement);
        }
        finally
        {
            var folder = Path.GetDirectoryName(path)!;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
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
