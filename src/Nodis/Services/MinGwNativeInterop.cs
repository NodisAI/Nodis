using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.JobObjects;
using Nodis.Interfaces;
using Nodis.Models;

namespace Nodis.Services;

[SupportedOSPlatform("windows5.1.2600")]
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
        ChildProcessTracker.AddProcess(process);

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

        public async Task<int> WaitAsync(CancellationToken cancellationToken)
        {
            try
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
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (!process.HasExited) process.Kill();
            }

            return process.ExitCode;
        }
    }

    /// <summary>
    ///     Allows processes to be automatically killed if this parent process unexpectedly quits.
    ///     This feature requires Windows 8 or greater. On Windows 7, nothing is done.
    /// </summary>
    /// <remarks>References:
    ///     https://stackoverflow.com/a/4657392/386091
    ///     https://stackoverflow.com/a/9164742/386091
    /// </remarks>
    private unsafe static class ChildProcessTracker
    {
        /// <summary>
        ///     Add the process to be tracked. If our current process is killed, the child processes
        ///     that we are tracking will be automatically killed, too. If the child process terminates
        ///     first, that's fine, too.</summary>
        /// <param name="process"></param>
        public static void AddProcess(Process process)
        {
            bool success = PInvoke.AssignProcessToJobObject((HANDLE)JobHandle.DangerousGetHandle(), (HANDLE)process.Handle);
            if (!success && !process.HasExited)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        // Windows will automatically close any open job handles when our process terminates.
        // This can be verified by using SysInternals' Handle utility. When the job handle
        // is closed, the child processes will be killed.
        private static readonly SafeHandle JobHandle;

        static ChildProcessTracker()
        {
            // This feature requires Windows 8 or later. To support Windows 7 requires
            // registry settings to be added if you are using Visual Studio plus an
            // app.manifest change.
            // https://stackoverflow.com/a/4232259/386091
            // https://stackoverflow.com/a/9507862/386091
            if (Environment.OSVersion.Version < new Version(6, 2))
                throw new NotSupportedException("This feature requires Windows 8 or later");

            // The job name is optional (and can be null) but it helps with diagnostics.
            // If it's not null, it has to be unique. Use SysInternals' Handle command-line
            // utility: handle -a ChildProcessTracker
            var jobName = "ChildProcessTracker" + Environment.ProcessId;
            JobHandle = PInvoke.CreateJobObject(null, jobName);

            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    // This is the key flag. When our process is killed, Windows will automatically
                    // close the job handle, and when that happens, we want the child processes to
                    // be killed, too.
                    LimitFlags = JOB_OBJECT_LIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
                },
            };

            var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            var extendedInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

                if (!PInvoke.SetInformationJobObject(
                        JobHandle,
                        JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                        extendedInfoPtr.ToPointer(),
                        (uint)length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }
    }
}