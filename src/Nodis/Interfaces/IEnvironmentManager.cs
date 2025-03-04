using Nodis.Models;

namespace Nodis.Interfaces;

public interface IEnvironmentManager
{
    IEnumerable<Metadata> EnumerateSources();

    IEnumerable<Metadata> EnumerateNodes();

    Task UpdateSourcesAsync(CancellationToken cancellationToken);

    Task<NodeMetadata> LoadNodeAsync(Metadata metadata, CancellationToken cancellationToken);

    Task InstallNodeAsync(Metadata metadata, CancellationToken cancellationToken);
}