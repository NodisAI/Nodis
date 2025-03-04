using Nodis.Models;

namespace Nodis.Interfaces;

public interface IEnvironmentManager
{
    public static string DataFolderPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(Nodis));

    IEnumerable<Metadata> EnumerateSources();

    IEnumerable<Metadata> EnumerateNodes();

    Task UpdateSourcesAsync(CancellationToken cancellationToken);

    Task<NodeMetadata> LoadNodeAsync(Metadata metadata, CancellationToken cancellationToken);

    Task InstallNodeAsync(Metadata metadata, CancellationToken cancellationToken);
}