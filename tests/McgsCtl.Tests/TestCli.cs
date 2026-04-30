using System.Diagnostics;
using System.Text;

namespace McgsCtl.Tests;

internal sealed record CliResult(int ExitCode, string Stdout, string Stderr)
{
    public string CombinedOutput => Stdout + Environment.NewLine + Stderr;
}

internal static class TestCli
{
    public static string ProjectPath => _projectPath.Value;

    private static readonly Lazy<string> _projectPath = new(FindProjectPath);

    public static CliResult Run(params string[] args) => Run(null, TimeSpan.FromSeconds(120), args);

    public static CliResult Run(string? workingDirectory, TimeSpan timeout, params string[] args)
    {
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(ProjectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(ProjectPath);
        start.ArgumentList.Add("--");
        foreach (var arg in args) start.ArgumentList.Add(arg);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("mcgsctl command timed out: " + string.Join(" ", args));
        }
        return new CliResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    private static string FindProjectPath()
    {
        var env = Environment.GetEnvironmentVariable("MCGSCTL_PROJECT");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var full = Path.GetFullPath(env);
            if (File.Exists(full)) return full;
            throw new FileNotFoundException("MCGSCTL_PROJECT does not exist: " + full);
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "McgsCtl.csproj");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find McgsCtl.csproj from AppContext.BaseDirectory=" + AppContext.BaseDirectory +
            "; CurrentDirectory=" + Environment.CurrentDirectory);
    }
}
