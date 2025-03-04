using Nodis.Models;

namespace Nodis.Interfaces;

public interface IRuntimeHost
{
    Task<IAsyncDisposable> EnsureRuntimesAsync(IEnumerable<NameAndVersionConstraint> runtimeConstraints, CancellationToken cancellationToken);
}