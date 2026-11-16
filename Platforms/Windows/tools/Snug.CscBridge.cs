using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

internal static class SnugCscBridge
{
    private static readonly string ProjectRoot = GetProjectRoot();
    private static readonly string DotnetRoot = Path.Combine(ProjectRoot, ".tools", "dotnet");
    private static readonly string DotnetHost = Path.Combine(DotnetRoot, "dotnet.exe");
    private static readonly string CompilerRoot = Path.Combine(DotnetRoot, "sdk", GetSdkVersion(), "Roslyn", "bincore");
    private static readonly string CompilerDll = Path.Combine(CompilerRoot, "csc.dll");
    private static readonly string RuntimeConfig = Path.Combine(CompilerRoot, "csc.runtimeconfig.json");
    private static readonly string Dependencies = Path.Combine(CompilerRoot, "csc.deps.json");

    private static int Main(string[] args)
    {
        if (!File.Exists(DotnetHost) || !File.Exists(CompilerDll))
        {
            Console.Error.WriteLine("Snug C# bridge: .NET SDK compiler files were not found under the local dotnet directory.");
            return 2;
        }

        var commandLine = new StringBuilder();
        commandLine.Append("exec --runtimeconfig ").Append(Quote(RuntimeConfig));
        commandLine.Append(" --depsfile ").Append(Quote(Dependencies));
        commandLine.Append(' ').Append(Quote(CompilerDll));
        foreach (var arg in args) commandLine.Append(' ').Append(Quote(arg));

        var start = new ProcessStartInfo
        {
            FileName = DotnetHost,
            Arguments = commandLine.ToString(),
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.EnvironmentVariables["DOTNET_ROOT"] = DotnetRoot;
        start.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";

        try
        {
            using (var process = Process.Start(start))
            {
                if (process == null) return 2;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                Console.Out.Write(stdout.Result);
                Console.Error.Write(stderr.Result);
                return process.ExitCode;
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Snug C# bridge: " + error);
            return 2;
        }
    }

    private static string GetProjectRoot()
    {
        var bridgeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var toolsDirectory = Directory.GetParent(bridgeDirectory).FullName;
        return Directory.GetParent(toolsDirectory).FullName;
    }

    private static string GetSdkVersion()
    {
        var globalJson = Path.Combine(ProjectRoot, "global.json");
        if (File.Exists(globalJson))
        {
            var content = File.ReadAllText(globalJson);
            var token = "\"version\"";
            var tokenIndex = content.IndexOf(token, StringComparison.Ordinal);
            if (tokenIndex >= 0)
            {
                var valueStart = content.IndexOf('"', tokenIndex + token.Length);
                var valueEnd = valueStart < 0 ? -1 : content.IndexOf('"', valueStart + 1);
                if (valueStart >= 0 && valueEnd > valueStart)
                    return content.Substring(valueStart + 1, valueEnd - valueStart - 1);
            }
        }
        return "10.0.404";
    }

    private static string Quote(string value)
    {
        if (value.Length == 0) return "\"\"";
        if (value.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0) return value;

        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\')
            {
                slashes++;
                continue;
            }
            if (ch == '"')
            {
                result.Append('\\', slashes * 2 + 1).Append('"');
                slashes = 0;
                continue;
            }
            result.Append('\\', slashes).Append(ch);
            slashes = 0;
        }
        result.Append('\\', slashes * 2).Append('"');
        return result.ToString();
    }
}
