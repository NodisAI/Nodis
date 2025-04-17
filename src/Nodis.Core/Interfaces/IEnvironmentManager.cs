using Nodis.Core.Models;

namespace Nodis.Core.Interfaces;

public interface IEnvironmentManager
{
    public static string DataFolderPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nodis");

    public static string CacheFolderPath { get; } = Path.Combine(DataFolderPath, "cache");

    /// <summary>
    /// Update sources that Marketplace uses to fetch bundles.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UpdateSourcesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Enumerate <see cref="Metadata"/> that represents all available <see cref="BundleManifest"/>
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<Metadata>> EnumerateBundleManifestMetadataAsync();

    /// <summary>
    /// Enumerate <see cref="Metadata"/> that represents all available installed <see cref="BundleManifest"/> (also <see cref="InstalledBundle"/>)
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<Metadata>> EnumerateInstalledBundleMetadataAsync();

    /// <summary>
    /// Loads a <see cref="BundleManifest"/> from the specified <see cref="Metadata"/> (e.g. From <see cref="EnumerateBundleManifestMetadataAsync"/>)
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<BundleManifest> LoadBundleManifestAsync(Metadata metadata, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a <see cref="BundleManifest"/> from the specified <see cref="Metadata"/> (e.g. From <see cref="EnumerateInstalledBundleMetadataAsync"/>)
    /// </summary>
    /// <param name="metadata"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<InstalledBundle> LoadInstalledBundleAsync(Metadata metadata, CancellationToken cancellationToken);

    /// <summary>
    /// Installs a <see cref="BundleManifest"/> from the specified <see cref="Metadata"/> (e.g. From <see cref="EnumerateBundleManifestMetadataAsync"/>)
    /// </summary>
    /// <remarks>
    /// NOTICE: All <see cref="ValueWithDescription{T}"/> should be filled before calling this method. It will use its DefaultValue as final value
    /// </remarks>
    /// <param name="metadata"></param>
    /// <param name="bundleManifest"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<InstalledBundle> InstallBundleAsync(Metadata metadata, BundleManifest bundleManifest, CancellationToken cancellationToken);

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