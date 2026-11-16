using System.Text.Json;

namespace Snug.Windows.Core;

public sealed class RestoreLedger(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath = Path.GetFullPath(filePath);

    public string FilePath => _filePath;

    public WindowRestoreRecord? Load()
    {
        if (!File.Exists(_filePath)) return null;
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<WindowRestoreRecord>(json, JsonOptions);
    }

    public void Save(WindowRestoreRecord record)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("还原记录路径没有父目录。");
        Directory.CreateDirectory(directory);
        var temporary = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(record, JsonOptions);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
            File.Move(temporary, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void Clear()
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
    }
}
