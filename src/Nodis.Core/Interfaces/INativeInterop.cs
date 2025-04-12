using System.Diagnostics.CodeAnalysis;
using Nodis.Core.Models;

namespace Nodis.Core.Interfaces;

public interface INativeInterop
{
    void OpenUri(Uri uri);

    [return: NotNullIfNotNull(nameof(path))]
    string? GetFullPath(string? path);

    IProcess CreateProcess(ProcessCreationOptions options);
}

public interface IProcess
{
    StreamReader StandardOutput { get; }

    StreamReader StandardError { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    void Kill();
}