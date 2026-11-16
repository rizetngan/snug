using System.Text.Json;

namespace Snug.Windows.Core;

/// <summary>
/// An app can destroy and recreate a window after Snug has resized it.
/// Match returned windows only when the executable and last Snug-filled
/// dimensions agree. Old HWNDs must never be used to restore another window.
/// Based on upstream LeftBehind.swift, with Windows-specific HWND identity.
/// </summary>
public sealed record LeftBehindEntry(WindowRestoreRecord Record, DateTimeOffset Since);

public sealed class LeftBehindBook
{
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(14);
    public const int Limit = 20;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _filePath;
    private List<LeftBehindEntry> _entries;

    public LeftBehindBook(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
        try
        {
            _entries = File.Exists(_filePath)
                ? JsonSerializer.Deserialize<List<LeftBehindEntry>>(File.ReadAllText(_filePath), JsonOptions) ?? []
                : [];
            _entries.RemoveAll(entry => entry.Record.FilledBounds is null ||
                string.IsNullOrWhiteSpace(entry.Record.ExecutablePath));
            PruneInMemory(DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // A corrupted history must not prevent Snug from starting; do not
            // modify the broken file until a subsequent successful save.
            _entries = [];
        }
    }

    public IReadOnlyList<LeftBehindEntry> Entries
    {
        get { lock (_gate) return _entries.ToArray(); }
    }

    public bool Add(WindowRestoreRecord record, DateTimeOffset now)
    {
        if (record.FilledBounds is not { Width: > 0, Height: > 0 } ||
            string.IsNullOrWhiteSpace(record.ExecutablePath))
            return false;

        lock (_gate)
        {
            _entries.RemoveAll(entry => entry.Record.Key == record.Key);
            _entries.Add(new LeftBehindEntry(record, now));
            PruneInMemory(now);
            Save();
            return true;
        }
    }

    public LeftBehindEntry? Match(WindowInfo window, WindowBounds actual, DateTimeOffset now)
    {
        lock (_gate)
        {
            // Keep expired records from matching, even if no write happened.
            var candidates = _entries.Where(entry =>
                now - entry.Since <= KeepFor &&
                entry.Record.FilledBounds is { } filled &&
                StringComparer.OrdinalIgnoreCase.Equals(
                    entry.Record.ExecutablePath, window.ExecutablePath) &&
                Math.Abs(filled.Width - actual.Width) < 2 &&
                Math.Abs(filled.Height - actual.Height) < 2).ToArray();

            static LeftBehindEntry? Latest(IEnumerable<LeftBehindEntry> items) =>
                items.OrderByDescending(entry => entry.Since).FirstOrDefault();

            return Latest(candidates.Where(entry => entry.Record.Key == window.Key))
                ?? Latest(candidates.Where(entry =>
                    !string.IsNullOrWhiteSpace(entry.Record.Title) &&
                    StringComparer.Ordinal.Equals(entry.Record.Title, window.Title)))
                ?? Latest(candidates);
        }
    }

    public void Remove(LeftBehindEntry entry)
    {
        lock (_gate)
        {
            if (_entries.Remove(entry)) Save();
        }
    }

    public void Prune(DateTimeOffset now)
    {
        lock (_gate)
        {
            var before = _entries.Count;
            PruneInMemory(now);
            if (before != _entries.Count) Save();
        }
    }

    private void PruneInMemory(DateTimeOffset now)
    {
        _entries.RemoveAll(entry => now - entry.Since > KeepFor);
        _entries = _entries.OrderBy(entry => entry.Since).TakeLast(Limit).ToList();
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Left-behind history path has no parent.");
        Directory.CreateDirectory(directory);
        var temporary = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(_entries, JsonOptions));
            File.Move(temporary, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}