using XtermSharp;

namespace Nodis.Backend.Interfaces;

public interface IProcess
{
    StreamWriter StandardInput { get; }

    StreamReader StandardOutput { get; }

    StreamReader StandardError { get; }

    bool HasExited { get; }

    Terminal CreateTerminal();

    Task StartAsync(CancellationToken cancellationToken);

    Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    void Kill();
}