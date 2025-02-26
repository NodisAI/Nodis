using System.Diagnostics;
using Nodis.Interfaces;
using Nodis.Models;

namespace Nodis.Services;

// ReSharper disable once InconsistentNaming
public class MinGWBashExecutor : IBashExecutor
{
    private string BashExecutablePath { get; } = Path.Combine(Environment.CurrentDirectory, "PortableGit", "bin", "bash.exe");

    public IBashExecution Execute(BashExecutionOptions options, CancellationToken cancellationToken = default)
    {
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

    private class BashExecution(Process process, BashExecutionOptions options) : IBashExecution
    {
        public Stream StandardOutput => process.StandardOutput.BaseStream;
        public Stream StandardError => process.StandardError.BaseStream;

        public async Task<int> WaitAsync()
        {
            var input = process.StandardInput;
            foreach (var (key, value) in options.EnvironmentVariables)
            {
                await input.WriteLineAsync($"export {key}={value}");
            }
            foreach (var commandLine in options.CommandLines)
            {
                await input.WriteLineAsync(commandLine);
            }

            await input.WriteLineAsync("exit");
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }
}