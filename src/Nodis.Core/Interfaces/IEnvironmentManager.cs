using Nodis.Core.Models;

namespace Nodis.Core.Interfaces;

public interface IEnvironmentManager
{
    public static string DataFolderPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "nodis");

    public static string CacheFolderPath { get; } = Path.Combine(DataFolderPath, "cache");

    Task<IEnumerable<Metadata>> EnumerateSourcesAsync();

    Task<IEnumerable<Metadata>> EnumeratePackagesAsync();

    Task UpdateSourcesAsync(CancellationToken cancellationToken);

    Task<PackageMetadata> LoadLocalNodeAsync(Metadata metadata, CancellationToken cancellationToken);

    Task<PackageMetadata> LoadSourcePackageAsync(Metadata metadata, CancellationToken cancellationToken);

    Task InstallPackageAsync(Metadata metadata, CancellationToken cancellationToken);

    /// <summary>
    /// Ensures that the specified runtimes are installed and running until <see cref="IAsyncDisposable"/> is disposed.
    /// </summary>
    /// <param name="namespace"></param>
    /// <param name="runtimeConstraintsList"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IAsyncDisposable> EnsureRuntimesAsync(
        string @namespace,
        IEnumerable<NameAndVersionConstraints> runtimeConstraintsList,
        CancellationToken cancellationToken);
}