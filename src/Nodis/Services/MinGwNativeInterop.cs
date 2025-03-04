using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Nodis.Interfaces;
using Nodis.Models;

namespace Nodis.Services;

[SupportedOSPlatform("windows")]
public class MinGwNativeInterop : INativeInterop
{
    private string BashExecutablePath { get; } = Path.Combine(Environment.CurrentDirectory, "PortableGit", "bin", "bash.exe");

    public void OpenUri(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    public IBashExecution BashExecute(BashExecutionOptions options)
    {
        options.EnvironmentVariables["OSTYPE"] = "msys";
        options.EnvironmentVariables["WORKING_DIR"] = GetUnixPath(options.WorkingDirectory) ?? "~";
        options.EnvironmentVariables["CACHE_DIR"] = GetUnixPath(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(Nodis), ".cache"));

        var startInfo = new ProcessStartInfo
        {
            FileName = BashExecutablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory
        };

        var process = new Process { StartInfo = startInfo };
        process.Start();

        return new BashExecution(process, options);
    }

    [return: NotNullIfNotNull(nameof(path))]
    private static string? GetUnixPath(string? path)
    {
        if (path == null) return path;
        path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        if (path.Length < 2) return path;
        var drive = path[0];
        path = path.Replace('\\', '/');
        return $"/{drive.ToString().ToLower()}{path[2..]}";
    }

    private class BashExecution(Process process, BashExecutionOptions options) : IBashExecution
    {
        public StreamReader StandardOutput => process.StandardOutput;
        public StreamReader StandardError => process.StandardError;

        public async Task<int> WaitAsync()
        {
            var input = process.StandardInput;
            foreach (var (key, value) in options.EnvironmentVariables)
            {
                await input.WriteLineAsync($"export {key}={value}");
            }

            if (options.ScriptPath is { } scriptPath)
            {
                await input.WriteLineAsync($"source {GetUnixPath(scriptPath)} {string.Join(' ', options.CommandLines)}");
            }
            else
            {
                foreach (var commandLine in options.CommandLines)
                {
                    await input.WriteLineAsync(commandLine);
                }
            }

            await input.WriteLineAsync("exit");
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }
}