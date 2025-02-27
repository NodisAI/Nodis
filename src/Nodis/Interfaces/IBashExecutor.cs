using Nodis.Models;

namespace Nodis.Interfaces;

public interface IBashExecutor
{
    IBashExecution Execute(BashExecutionOptions options, CancellationToken cancellationToken = default);
}

public interface IBashExecution
{
    StreamReader StandardOutput { get; }

    StreamReader StandardError { get; }

    Task<int> WaitAsync();
}