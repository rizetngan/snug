namespace Snug.Windows.Core;

/// <summary>
/// Detects native confirmation/save dialogs without mistaking ordinary editor
/// windows for modal questions. A Win32 #32770 owned popup is authoritative;
/// other app frameworks require both ownership and a known prompt title.
/// </summary>
public static class DialogClassifier
{
    private static readonly string[] PromptWords =
    [
        "save", "保存", "另存为", "discard", "don't save",
        "do you want", "confirm", "confirmation", "确认",
        "replace", "overwrite", "覆盖", "warning", "警告",
        "alert", "are you sure", "确定要", "修改内容"
    ];

    public static bool IsAskingWindow(string className, string title, bool hasOwner)
    {
        if (!hasOwner) return false;
        if (string.Equals(className, "#32770", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.IsNullOrWhiteSpace(title)) return false;
        return PromptWords.Any(word =>
            title.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
