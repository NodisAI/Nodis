using System.Diagnostics.CodeAnalysis;
using Nodis.Core.Models;

namespace Nodis.Backend.Interfaces;

public interface INativeInterop
{
    void OpenUri(Uri uri);

    [return: NotNullIfNotNull(nameof(path))]
    string? GetFullPath(string? path);

    IProcess CreateProcess(ProcessCreationOptions options);
}