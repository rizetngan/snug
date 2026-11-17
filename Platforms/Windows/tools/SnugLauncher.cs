using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class SnugLauncher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string runtimeRoot = Path.Combine(root, "Runtime");
        string dotnet = Path.Combine(runtimeRoot, "dotnet.exe");
        string application = Path.Combine(root, "Snug.Windows.dll");

        if (!File.Exists(dotnet) || !File.Exists(application))
        {
            MessageBox.Show(
                "Snug 运行文件不完整，请重新安装 Snug。",
                "Snug",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        try
        {
            string arguments = "\"" + application + "\"";
            if (args != null && args.Length > 0)
                arguments += " " + String.Join(" ", args);

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = dotnet,
                Arguments = arguments,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            start.EnvironmentVariables["DOTNET_ROOT"] = runtimeRoot;
            start.EnvironmentVariables["DOTNET_ROOT_X64"] = runtimeRoot;
            start.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";

            Process process = Process.Start(start);
            if (process == null)
                throw new InvalidOperationException("无法启动 Snug。");
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show(
                "Snug 启动失败：\r\n" + error.Message,
                "Snug",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }
}