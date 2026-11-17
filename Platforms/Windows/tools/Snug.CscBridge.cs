using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

internal static class SnugCscBridge
{
    private static readonly string ProjectRoot = GetProjectRoot();
    private static readonly string DotnetRoot = ResolveDotnetRoot();
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

    private static string ResolveDotnetRoot()
    {
        var configured = Environment.GetEnvironmentVariable("SNUG_DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        var local = Path.Combine(ProjectRoot, ".tools", "dotnet");
        if (File.Exists(Path.Combine(local,"dotnet.exe"))) return local;
        configured = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet");
    }

    private static string GetSdkVersion()
    {
        // The SDK resolver honors global.json including latestPatch. Use
        // the same host and repository directory as the invoking build.
        using (var process = Process.Start(new ProcessStartInfo
        {
            FileName = DotnetHost, Arguments = "--version",
            WorkingDirectory = ProjectRoot, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true
        }))
        {
            if (process == null) throw new InvalidOperationException("Cannot resolve .NET SDK.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            var version = output.Result.Trim();
            if (process.ExitCode != 0 || !System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"))
                throw new InvalidOperationException("Cannot resolve pinned .NET SDK: " + error.Result);
            return version;
        }
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
