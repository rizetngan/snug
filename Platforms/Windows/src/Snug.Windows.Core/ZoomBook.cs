namespace Snug.Windows.Core;

public sealed class ZoomBook
{
    public const int MinimumSteps = -4;
    public const int MaximumSteps = 8;

    private readonly Dictionary<string, int> _remembered = new(StringComparer.OrdinalIgnoreCase);

    public ZoomBook(IEnumerable<KeyValuePair<string, int>>? remembered = null)
    {
        if (remembered is null) return;
        foreach (var entry in remembered)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)) continue;
            try
            {
                var key = entry.Key.StartsWith("app:", StringComparison.OrdinalIgnoreCase)
                    ? "app:" + entry.Key[4..].Trim().ToLowerInvariant()
                    : Normalize(entry.Key);
                _remembered[key] = Math.Clamp(entry.Value, MinimumSteps, MaximumSteps);
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
        }
    }

    public IReadOnlyDictionary<string, int> Remembered => _remembered;

    public bool TryGetRemembered(string executablePath, out int steps)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) { steps = 0; return false; }
        try { return _remembered.TryGetValue(Normalize(executablePath), out steps); }
        catch (ArgumentException) { steps = 0; return false; }
        catch (NotSupportedException) { steps = 0; return false; }
    }

    public int StepsFor(string executablePath, int defaultSteps) =>
        StepsFor(executablePath, string.Empty, defaultSteps);

    public int StepsFor(string executablePath, string appName, int defaultSteps)
    {
        var key = KeyFor(executablePath, appName);
        return _remembered.TryGetValue(key, out var steps)
            ? Math.Clamp(steps, MinimumSteps, MaximumSteps)
            : Math.Clamp(defaultSteps, MinimumSteps, MaximumSteps);
    }

    public void Remember(string executablePath, int steps) =>
        Remember(executablePath, string.Empty, steps);

    public void Remember(string executablePath, string appName, int steps)
    {
        _remembered[KeyFor(executablePath, appName)] = Math.Clamp(steps, MinimumSteps, MaximumSteps);
    }

    public void ForgetAll() => _remembered.Clear();

    public static bool IsZoomable(string executablePath) =>
        IsZoomable(executablePath, string.Empty);

    public static bool IsZoomable(string executablePath, string appName)
    {
        var name = Path.GetFileName(executablePath);
        if (string.IsNullOrWhiteSpace(name)) name = appName;
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name += ".exe";

        return name.Equals("Cursor.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Code.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Zed.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Antigravity.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("wezterm-gui.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("claude.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string KeyFor(string executablePath, string appName)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
            return Normalize(executablePath);
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("An executable path or application name is required.");
        return "app:" + appName.Trim().ToLowerInvariant();
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
}
