using System.Text.RegularExpressions;

namespace Snug.Windows.Core;

/// <summary>Ports upstream ControllerKind.swift name hints. Only call for a proven local controller row.</summary>
public static class ControllerClassifier
{
    private static readonly string[] MobileHints =
        ["iphone", "ipad", "matepad", "android", "phone", "手机", "平板"];
    private static readonly string[] ComputerHints =
        ["mac", "desktop-", "thinkpad", "pc", "windows", "matebook", "magicbook",
         "redmibook", "xiaomi book", "galaxy book", "surface", "电脑", "笔记本", "台式"];

    public static ControllerKind Classify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ControllerKind.Unknown;
        var normalized = name.ToLowerInvariant();
        var words = Regex.Split(normalized, @"[^\p{L}\p{N}]+");
        if (MobileHints.Any(normalized.Contains) || words.Contains("pad"))
            return ControllerKind.Phone;
        if (ComputerHints.Any(normalized.Contains) || words.Contains("laptop") || words.Contains("notebook"))
            return ControllerKind.Desktop;
        return ControllerKind.Unknown;
    }
}