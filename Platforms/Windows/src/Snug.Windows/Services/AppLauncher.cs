using System.Diagnostics;
using System.IO;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

public static class AppLauncher
{
    public static Process? Launch(AppInfo app)
    {
        var target = app.LaunchTarget;
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException($"找不到「{app.Name}」的启动入口。");

        if (target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            var shellTarget = target;
            return Process.Start(new ProcessStartInfo("explorer.exe", shellTarget)
            {
                UseShellExecute = true
            });
        }

        if (!Path.IsPathFullyQualified(target) && !target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"「{app.Name}」的启动路径无效：{target}");

        return Process.Start(new ProcessStartInfo(target)
        {
            Arguments = app.LaunchArguments ?? string.Empty,
            WorkingDirectory = Path.IsPathFullyQualified(target)
                ? Path.GetDirectoryName(target) ?? Environment.CurrentDirectory
                : Environment.CurrentDirectory,
            UseShellExecute = true
        });
    }
}
