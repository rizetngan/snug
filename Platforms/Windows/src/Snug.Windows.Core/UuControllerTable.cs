namespace Snug.Windows.Core;

public enum UuElementKind { Text, Table, Row, Other }
public sealed record UuElement(UuElementKind Kind, string Text = "",
    IReadOnlyList<UuElement>? Children = null)
{
    public IReadOnlyList<UuElement> Items => Children ?? [];
}

/// <summary>Only a controller table directly beside the local controlled-page title is trusted.
/// A device detail page for another machine can otherwise give identical remote-control rows.</summary>
public static class UuControllerTable
{
    private const string PageTitle = "本机被控中";
    private const string ControlMark = "远程控制";
    private const string DurationPrefix = "连接时长";

    public static IReadOnlyList<ControllerInfo> Read(IEnumerable<UuElement> windows)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ControllerInfo>();
        foreach (var table in windows.SelectMany(LocalTables))
        foreach (var row in Descendants(table).Where(node => node.Kind == UuElementKind.Row))
        {
            var texts = Descendants(row).Where(node => node.Kind == UuElementKind.Text)
                .Select(node => node.Text.Trim()).ToArray();
            if (!texts.Contains(ControlMark)) continue;
            var name = texts.FirstOrDefault(text => text.Length > 0 && text != ControlMark
                && !text.StartsWith(DurationPrefix, StringComparison.Ordinal));
            if (name is not null && names.Add(name))
                result.Add(new ControllerInfo(name, ControllerClassifier.Classify(name)));
        }
        return result;
    }

    private static IEnumerable<UuElement> LocalTables(UuElement element)
    {
        if (element.Items.Any(node => node.Kind == UuElementKind.Text && node.Text.Trim() == PageTitle))
            foreach (var table in element.Items.Where(node => node.Kind == UuElementKind.Table))
                yield return table;
        foreach (var child in element.Items)
            foreach (var table in LocalTables(child))
                yield return table;
    }

    private static IEnumerable<UuElement> Descendants(UuElement element)
    {
        yield return element;
        foreach (var child in element.Items)
            foreach (var desc in Descendants(child))
                yield return desc;
    }
}