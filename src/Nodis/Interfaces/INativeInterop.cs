using Nodis.Models;

namespace Nodis.Interfaces;

public interface INativeInterop
{
    void OpenUri(Uri uri);

    IBashExecution BashExecute(BashExecutionOptions options);
}

public interface IBashExecution
{
    StreamReader StandardOutput { get; }

    StreamReader StandardError { get; }

    Task<int> WaitAsync();
}