using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.JobObjects;
using Nodis.Core.Extensions;
using Nodis.Core.Interfaces;
using Nodis.Core.Models;

namespace Nodis.Backend.Services;

[SupportedOSPlatform("windows5.1.2600")]
public class WindowsNativeInterop : INativeInterop
{
    /// <summary>
    /// we use MSYS2's bash as the default shell
    /// </summary>
    private string BashExecutablePath { get; } = Path.Combine(Environment.CurrentDirectory, "PortableGit", "bin", "bash.exe");

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

    public void OpenUri(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    [return: NotNullIfNotNull(nameof(path))]
    public string? GetFullPath(string? path) => GetUnixPath(path);

    public IProcess CreateProcess(ProcessCreationOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.Type == ProcessStartType.Bash ? BashExecutablePath : options.Executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = options.WorkingDirectory
        };

        if (options.Type == ProcessStartType.Normal)
        {
            startInfo.ArgumentList.AddRange(options.Arguments);
            foreach (var (key, value) in options.EnvironmentVariables) startInfo.EnvironmentVariables[key] = value;
        }
        else
        {
            options.EnvironmentVariables["OSTYPE"] = "msys";
            options.EnvironmentVariables["WORKING_DIR"] = GetUnixPath(options.WorkingDirectory) ?? "~";
            options.EnvironmentVariables["CACHE_DIR"] = GetUnixPath(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(Core), "Cache", "MinGW"));
        }

        var process = new Process { StartInfo = startInfo };
        return options.Type == ProcessStartType.Normal ?
            new NormalProcess(process, options) :
            new BashProcess(process, options);
    }

    private class NormalProcess(Process process, ProcessCreationOptions options) : IProcess
    {
        protected readonly Process process = process;
        protected readonly ProcessCreationOptions options = options;
        private bool isStarted;

        public StreamReader StandardOutput => process.StandardOutput;
        public StreamReader StandardError => process.StandardError;

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    process.Start();
                    if (options.KillOnExit) ChildProcessTracker.AddProcess(process);
                    isStarted = true;
                }, cancellationToken);
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            if (!isStarted) await StartAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }

        public void Kill()
        {
            process.Kill();
        }
    }

    private class BashProcess(Process process, ProcessCreationOptions options) : NormalProcess(process, options)
    {
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            var input = process.StandardInput;
            foreach (var (key, value) in options.EnvironmentVariables)
            {
                await input.WriteLineAsync($"export {key}={value}");
            }

            if (options.Executable is { } scriptPath)
            {
                await input.WriteLineAsync($"source {GetUnixPath(scriptPath)} {string.Join(' ', options.Arguments)}");
            }
            else
            {
                foreach (var argument in options.Arguments)
                {
                    await input.WriteLineAsync(argument);
                }
            }

            await input.WriteLineAsync("exit");
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
            lock (JobHandle)
            {
                bool success = PInvoke.AssignProcessToJobObject((HANDLE)JobHandle.DangerousGetHandle(), (HANDLE)process.Handle);
                if (!success && !process.HasExited)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
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