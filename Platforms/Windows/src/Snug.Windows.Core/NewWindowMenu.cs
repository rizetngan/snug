namespace Snug.Windows.Core;

/// <summary>
/// Original Snug NewWindowMenu selection rules adapted to Windows native menu commands.
/// Never treat a generic 'New' or 'New file' command as a window command.
/// </summary>
public static class NewWindowMenu
{
    public sealed record Item(
        string Title,
        uint CommandId,
        string? Parent = null,
        bool Enabled = true,
        bool HasSubmenu = false);

    public enum Shortcut { None, CtrlN, CtrlShiftN }

    private static readonly HashSet<string> BrowserExecutables =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome", "msedge", "firefox", "brave", "vivaldi", "opera"
        };

    private static readonly HashSet<string> EditorExecutables =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "code", "code-insiders", "codium", "cursor"
        };

    public static bool WorthOpening(string title) => MentionsWindow(title);

    public static Item? Pick(IEnumerable<Item> items)
    {
        var commands = items.Where(item =>
            item.Enabled && !item.HasSubmenu && item.CommandId is > 0 and < 65536).ToArray();
        if (commands.Length == 0) return null;
        return commands.Where(item => IsNewWindow(item.Title)
            || item.Parent is { } parent && IsNewWindow(parent))
            .OrderBy(item => HasShortcut(item.Title) ? 0 : 1)
            .FirstOrDefault()
            ?? commands.Where(item => StartsNew(item.Title) && MentionsWindow(item.Title))
                .OrderBy(item => HasShortcut(item.Title) ? 0 : 1)
                .FirstOrDefault()
            ?? commands.Where(item => MentionsWindow(item.Title) && HasShortcut(item.Title))
                .FirstOrDefault();
    }

    public static Shortcut KnownShortcut(AppInfo app)
    {
        var path = app.ExecutablePath ?? app.LaunchTarget ?? app.AppId;
        var file = Path.GetFileNameWithoutExtension(path).Trim();
        if (BrowserExecutables.Contains(file) || file.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            return Shortcut.CtrlN;
        if (EditorExecutables.Contains(file)) return Shortcut.CtrlShiftN;
        return Shortcut.None;
    }

    private static bool IsNewWindow(string title)
    {
        var text = Normalize(title);
        return text is "new window" or "新建窗口" or "新窗口"
            or "建立新視窗" or "新增視窗" or "新建视窗" or "open new window";
    }

    private static bool StartsNew(string title)
    {
        var text = Normalize(title);
        return text.StartsWith("new ", StringComparison.Ordinal) ||
               text.StartsWith("open new ", StringComparison.Ordinal) ||
               text.StartsWith("新建", StringComparison.Ordinal) ||
               text.StartsWith("新增", StringComparison.Ordinal) ||
               text.StartsWith("建立新", StringComparison.Ordinal);
    }

    private static bool MentionsWindow(string title)
    {
        var text = Normalize(title);
        return text.Contains("window", StringComparison.Ordinal) ||
               text.Contains("窗口", StringComparison.Ordinal) ||
               text.Contains("视窗", StringComparison.Ordinal) ||
               text.Contains("視窗", StringComparison.Ordinal);
    }

    private static bool HasShortcut(string title)
    {
        var text = title.ToLowerInvariant();
        return text.Contains("ctrl+n", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ctrl + n", StringComparison.OrdinalIgnoreCase)
            || text.Contains("⌘n", StringComparison.Ordinal);
    }

    private static string Normalize(string title)
    {
        var name = (title ?? string.Empty).Split('\t')[0]
            .Trim().TrimEnd('…', '.', ' ', '。').Replace("&", "");
        return name.ToLowerInvariant();
    }
}
