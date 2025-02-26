using Nodis.Models;

namespace Nodis.Interfaces;

public interface IBashExecutor
{
    IBashExecution Execute(BashExecutionOptions options, CancellationToken cancellationToken = default);
}

public interface IBashExecution
{
    Stream StandardOutput { get; }

    Stream StandardError { get; }

    Task<int> WaitAsync();
}