using System.Diagnostics;
using System.Windows.Automation;
using Snug.Windows.Core;
using Snug.Windows.Interop;

namespace Snug.Windows.Services;

/// <summary>Read-only, bounded, best-effort adapter for UU's own Windows accessibility tree.
/// Never foregrounds UU, clicks navigation, samples a different device page, or reads credentials.
/// Qt versions with no useful accessibility provider remain unreadable: logs remain independent.
/// </summary>
internal sealed class UuControllerReader
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(2);
    private readonly object _gate = new();
    private bool _inFlight;
    private DateTimeOffset _lastReadAt = DateTimeOffset.MinValue;
    private IReadOnlyList<ControllerInfo> _latest = [];
    private string _status = "未读取";

    public (IReadOnlyList<ControllerInfo> Controllers, string Status) Sample()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_inFlight && now - _lastReadAt >= TimeSpan.FromMilliseconds(500))
            {
                _inFlight = true;
                _ = Task.Run(Read);
            }
            return now - _lastReadAt <= FreshFor
                ? (_latest, _status)
                : ([], _inFlight ? "读取中或超时" : "不可读取");
        }
    }

    private void Read()
    {
        var watch = Stopwatch.StartNew();
        IReadOnlyList<ControllerInfo> result = [];
        var status = "本机控制端表格不可读取";
        try
        {
            var procs = Process.GetProcessesByName("GameViewer");
            try
            {
                var pids = procs.Select(p => (uint)p.Id).ToHashSet();
                var handles = new List<IntPtr>();
                NativeMethods.EnumWindows((handle, _) =>
                {
                    NativeMethods.GetWindowThreadProcessId(handle, out var pid);
                    if (pids.Contains(pid) && NativeMethods.IsWindowVisible(handle)
                        && !NativeMethods.IsHungAppWindow(handle))
                        handles.Add(handle);
                    return true;
                }, IntPtr.Zero);
                var roots = new List<UuElement>();
                var budget = 600;
                foreach (var handle in handles)
                {
                    if (watch.Elapsed > FreshFor || budget <= 0) break;
                    var root = AutomationElement.FromHandle(handle);
                    if (root is not null)
                        roots.Add(Snapshot(root, 0, ref budget, watch));
                }
                if (watch.Elapsed <= FreshFor)
                {
                    result = UuControllerTable.Read(roots);
                    if (result.Count > 0) status = $"本机控制端已识别（{result.Count}）";
                }
            }
            finally { foreach (var proc in procs) proc.Dispose(); }
        }
        catch (Exception error) when (error is ElementNotAvailableException
            or InvalidOperationException or System.Runtime.InteropServices.COMException
            or ArgumentException or System.ComponentModel.Win32Exception)
        {
            status = "本机控制端表格不可读取";
        }
        finally
        {
            lock (_gate)
            {
                _latest = watch.Elapsed <= FreshFor ? result : [];
                _status = watch.Elapsed <= FreshFor ? status : "控制端读取超时";
                _lastReadAt = DateTimeOffset.UtcNow;
                _inFlight = false;
            }
        }
    }

    private static UuElement Snapshot(AutomationElement element, int depth,
        ref int budget, Stopwatch watch)
    {
        if (depth > 12 || --budget < 0 || watch.Elapsed > FreshFor)
            return new(UuElementKind.Other);
        var current = element.Current;
        var type = current.ControlType;
        var kind = type == ControlType.Text ? UuElementKind.Text
            : type == ControlType.Table || type == ControlType.DataGrid ? UuElementKind.Table
            : type == ControlType.DataItem || type == ControlType.ListItem ? UuElementKind.Row
            : UuElementKind.Other;
        var text = kind == UuElementKind.Text ? current.Name : "";
        var children = new List<UuElement>();
        var walker = TreeWalker.ControlViewWalker;
        for (var child = walker.GetFirstChild(element); child is not null
            && budget > 0 && watch.Elapsed <= FreshFor; child = walker.GetNextSibling(child))
            children.Add(Snapshot(child, depth + 1, ref budget, watch));
        return new(kind, text ?? "", children);
    }
}