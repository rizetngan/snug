using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>读取 Windows 开始菜单与 AppsFolder，建立本机可启动 App 目录。</summary>
public sealed class InstalledAppScanner
{
    public Task<IReadOnlyList<InstalledApp>> ScanAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<InstalledApp>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(AppList.Sorted(Scan())); }
            catch (Exception error) { completion.TrySetException(error); }
        })
        {
            IsBackground = true,
            Name = "Snug App Catalog"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static IReadOnlyList<InstalledApp> Scan()
    {
        var apps = new List<InstalledApp>();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ScanShortcuts(apps, identities, names);
        ScanAppsFolder(apps, identities, names);
        return apps;
    }

    private static void ScanShortcuts(
        ICollection<InstalledApp> apps,
        ISet<string> identities,
        ISet<string> names)
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs")
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;
        object? shell = null;

        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return;
            dynamic automation = shell;

            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                IEnumerable<string> shortcuts;
                try { shortcuts = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories).ToArray(); }
                catch (UnauthorizedAccessException) { continue; }
                catch (IOException) { continue; }

                foreach (var shortcutPath in shortcuts)
                {
                    object? shortcut = null;
                    try
                    {
                        shortcut = automation.CreateShortcut(shortcutPath);
                        dynamic item = shortcut;
                        var target = Convert.ToString(item.TargetPath)?.Trim() ?? string.Empty;
                        var arguments = Convert.ToString(item.Arguments)?.Trim() ?? string.Empty;
                        var name = Path.GetFileNameWithoutExtension(shortcutPath).Trim();
                        if (target.Length == 0 || name.Length == 0) continue;

                        var aumid = FindAumid(arguments);
                        var executable = IsExecutable(target) ? NormalizePath(target) : null;
                        var appId = aumid is not null ? "aumid:" + aumid
                            : executable is not null ? PathId(executable)
                            : "link:" + NormalizePath(shortcutPath);
                        if (!identities.Add(appId)) continue;

                        var icon = ParseIconPath(Convert.ToString(item.IconLocation));
                        if (icon is null && executable is not null) icon = executable;
                        var englishName = ProductName(executable) ?? name;
                        var launchTarget = aumid is not null && !IsExecutable(target)
                            ? "shell:AppsFolder\\" + aumid
                            : target;

                        apps.Add(new InstalledApp(new AppInfo(
                            appId, name, executable, launchTarget, englishName, icon, arguments)));
                        names.Add(SearchKeys.Normalize(name));
                    }
                    catch (Exception error) when (error is COMException or InvalidCastException
                        or RuntimeBinderException or IOException or UnauthorizedAccessException
                        or ArgumentException or NotSupportedException)
                    {
                        // 单个快捷方式损坏不影响其余 App。
                    }
                    finally
                    {
                        ReleaseCom(shortcut);
                    }
                }
            }
        }
        finally
        {
            ReleaseCom(shell);
        }
    }

    private static void ScanAppsFolder(
        ICollection<InstalledApp> apps,
        ISet<string> identities,
        ISet<string> names)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return;
        object? shell = null;
        object? folder = null;
        object? items = null;

        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return;
            dynamic automation = shell;
            folder = automation.Namespace("shell:AppsFolder");
            if (folder is null) return;
            dynamic appsFolder = folder;
            items = appsFolder.Items();
            if (items is null) return;
            dynamic collection = items;
            var count = Math.Clamp(Convert.ToInt32(collection.Count), 0, 10000);

            for (var index = 0; index < count; index++)
            {
                object? entry = null;
                try
                {
                    entry = collection.Item(index);
                    if (entry is null) continue;
                    dynamic item = entry;
                    var name = Convert.ToString(item.Name)?.Trim() ?? string.Empty;
                    var path = Convert.ToString(item.Path)?.Trim() ?? string.Empty;
                    if (name.Length == 0 || path.Length == 0) continue;

                    // 已有开始菜单快捷方式时优先保留它提供的真实 exe 路径和图标。
                    if (names.Contains(SearchKeys.Normalize(name))) continue;

                    string aumid = string.Empty;
                    try { aumid = Convert.ToString(item.ExtendedProperty("System.AppUserModel.ID"))?.Trim() ?? string.Empty; }
                    catch (COMException) { }

                    var executable = IsExecutable(path) ? NormalizePath(path) : null;
                    var appIdentity = !string.IsNullOrWhiteSpace(aumid) ? aumid : path;
                    var appId = executable is not null ? PathId(executable) : "aumid:" + appIdentity;
                    if (!identities.Add(appId)) continue;

                    var launchTarget = executable is not null
                        ? executable
                        : "shell:AppsFolder\\" + appIdentity;
                    var englishName = ProductName(executable) ?? name;
                    apps.Add(new InstalledApp(new AppInfo(
                        appId, name, executable, launchTarget, englishName,
                        executable, string.Empty)));
                    names.Add(SearchKeys.Normalize(name));
                }
                catch (Exception error) when (error is COMException or InvalidCastException
                    or RuntimeBinderException or ArgumentException or NotSupportedException)
                {
                    // 某个 Shell 项不可读时继续枚举。
                }
                finally
                {
                    ReleaseCom(entry);
                }
            }
        }
        finally
        {
            ReleaseCom(items);
            ReleaseCom(folder);
            ReleaseCom(shell);
        }
    }

    private static bool IsExecutable(string path)
    {
        if (!Path.IsPathFullyQualified(path)) return false;
        try { return File.Exists(path) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase); }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    private static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
        catch (ArgumentException) { return path; }
        catch (NotSupportedException) { return path; }
    }

    private static string PathId(string path) => "exe:" + NormalizePath(path);

    private static string? FindAumid(string arguments)
    {
        const string marker = "shell:AppsFolder\\";
        var index = arguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var value = arguments[(index + marker.Length)..].Trim().Trim('"', '\\', '/');
        var end = value.IndexOfAny([' ', '"']);
        if (end >= 0) value = value[..end];
        return value.Length == 0 ? null : value;
    }

    private static string? ParseIconPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var path = value.Trim().Trim('"');
        var comma = path.LastIndexOf(',');
        if (comma > 1 && int.TryParse(path[(comma + 1)..].Trim(), out _))
            path = path[..comma].Trim().Trim('"');
        path = Environment.ExpandEnvironmentVariables(path);
        return File.Exists(path) ? path : null;
    }

    private static string? ProductName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var name = FileVersionInfo.GetVersionInfo(path).ProductName?.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); }
            catch (InvalidComObjectException) { }
        }
    }
}
