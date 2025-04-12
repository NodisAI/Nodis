using System.Buffers;
using System.Runtime.InteropServices;
using Nodis.Core.Extensions;
using Nodis.Core.Interfaces;
using Nodis.Core.Models;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Backend.Services;

public class LocalEnvironmentManager(
    IHttpClientFactory httpClientFactory,
    INativeInterop nativeInterop,
    YamlSerializerOptions yamlSerializerOptions
) : IEnvironmentManager
{
    private static string SourcesFolderPath => Path.Combine(IEnvironmentManager.DataFolderPath, "sources");
    private static string PackagesFolderPath => Path.Combine(IEnvironmentManager.DataFolderPath, "packages");

    private readonly HttpClient httpClient = httpClientFactory.CreateClient("global");

    public Task<IEnumerable<Metadata>> EnumerateSourcesAsync() => Task.FromResult(EnumerateSources());

    private static IEnumerable<Metadata> EnumerateSources()
    {
        if (!Directory.Exists(SourcesFolderPath)) yield break;
        foreach (var metadataFilePath in Directory
                     .EnumerateDirectories(SourcesFolderPath)
                     .SelectMany(Directory.EnumerateDirectories)
                     .Select(p => Path.Combine(p, "packages"))
                     .Where(Directory.Exists)
                     .SelectMany(Directory.EnumerateDirectories)
                     .SelectMany(Directory.EnumerateFiles))
        {
            if (!SemanticVersion.TryParse(Path.GetFileNameWithoutExtension(metadataFilePath), out var version)) continue;
            var folderParts = metadataFilePath.Split(Path.DirectorySeparatorChar);
            if (folderParts[^5].Contains('.') || folderParts[^4].Contains('.')) continue;
            yield return new Metadata($"{folderParts[^5]}.{folderParts[^4]}", folderParts[^2], version);
        }
    }

    public Task<IEnumerable<Metadata>> EnumeratePackagesAsync() => Task.FromResult(EnumeratePackages());

    private static IEnumerable<Metadata> EnumeratePackages()
    {
        var indexFilePath = Path.Combine(PackagesFolderPath, "index.yaml");
        if (!File.Exists(indexFilePath)) yield break;
        // todo
    }

    private async static ValueTask<IReadOnlyList<SourceIndexEntry>> LoadSourceIndexEntriesAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(SourcesFolderPath);
        var indexJsonPath = Path.Combine(SourcesFolderPath, "index.yaml");
        IReadOnlyList<SourceIndexEntry> entries;
        try
        {
            await using var fs = File.OpenRead(indexJsonPath);
            var buffer = new byte[fs.Length];
            if (await fs.ReadAsync(buffer, cancellationToken) != buffer.Length) throw new IOException("Failed to read index file");
            entries = YamlSerializer.Deserialize<IReadOnlyList<SourceIndexEntry>>(buffer);
        }
        catch
        {
            entries = [SourceIndexEntry.Main];
            await using var fs = File.Create(indexJsonPath);
            await fs.WriteAsync(YamlSerializer.Serialize(entries), cancellationToken);
        }

        return entries;
    }

    /// <summary>
    /// Use git to update all sources.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="Exception"></exception>
    public async Task UpdateSourcesAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(SourcesFolderPath);
        foreach (var (url, @namespace) in await LoadSourceIndexEntriesAsync(cancellationToken))
        {
            var relativePath = @namespace.Replace('.', Path.DirectorySeparatorChar);
            var sourceFolderPath = Path.Combine(SourcesFolderPath, relativePath);
            Directory.CreateDirectory(sourceFolderPath);

            var process = nativeInterop.CreateProcess(
                new ProcessCreationOptions
                {
                    Type = ProcessStartType.Bash,
                    Arguments = ["git pull"],
                    WorkingDirectory = sourceFolderPath
                });
            var result = await process.WaitForExitAsync(cancellationToken);
            // 128 means the folder is not a git repository
            // see https://stackoverflow.com/questions/4917871/does-git-return-specific-return-error-codes
            if (result == 128)
            {
                RecursivelyDeleteDirectory(sourceFolderPath);
                process = nativeInterop.CreateProcess(
                    new ProcessCreationOptions
                    {
                        Type = ProcessStartType.Bash,
                        Arguments = [$"git clone {url} {relativePath.Replace('\\', '/')}"],
                        WorkingDirectory = SourcesFolderPath
                    });
                result = await process.WaitForExitAsync(cancellationToken);
                if (result != 0) throw new Exception($"Failed to clone {url}\n{await process.StandardError.ReadToEndAsync(cancellationToken)}");
            }
        }
    }

    /// <summary>
    /// recursively delete a directory, with FileAttributes set to Normal.
    /// This can sometimes be necessary to delete a directory that contains read-only files.
    /// </summary>
    /// <param name="target"></param>
    public static void RecursivelyDeleteDirectory(string target)
    {
        foreach (var file in Directory.EnumerateFiles(target))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        foreach (var directory in Directory.EnumerateDirectories(target))
        {
            RecursivelyDeleteDirectory(directory);
        }
        File.SetAttributes(target, FileAttributes.Normal);
        Directory.Delete(target, false);
    }

    public async Task<PackageMetadata> LoadLocalNodeAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var nodeMetadataPath = Path.Combine(
            PackagesFolderPath,
            metadata.Namespace.Replace('.', Path.DirectorySeparatorChar),
            metadata.Name,
            metadata.Version + ".yaml");
        await using var fs = File.OpenRead(nodeMetadataPath);
        return await YamlSerializer.DeserializeAsync<PackageMetadata>(fs, yamlSerializerOptions);
    }

    public async Task<PackageMetadata> LoadSourcePackageAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var nodeMetadataPath = Path.Combine(
            SourcesFolderPath,
            metadata.Namespace.Replace('.', Path.DirectorySeparatorChar),
            "packages",
            metadata.Name,
            metadata.Version + ".yaml");
        await using var fs = File.OpenRead(nodeMetadataPath);
        return await YamlSerializer.DeserializeAsync<PackageMetadata>(fs, yamlSerializerOptions);
    }

    private async static ValueTask<string> SearchScriptAsync(string scriptName, CancellationToken cancellationToken)
    {
        var indexEntries = await LoadSourceIndexEntriesAsync(cancellationToken);
        foreach (var entry in indexEntries)
        {
            var scriptPath = Path.Combine(
                SourcesFolderPath,
                entry.Namespace.Replace('.', Path.DirectorySeparatorChar),
                "scripts",
                scriptName + ".sh");
            if (File.Exists(scriptPath)) return scriptPath;
        }

        throw new FileNotFoundException($"Script {scriptName} not found.");
    }

    private async ValueTask ExecuteInstallOperationAsync(
        string installationFolderPath,
        RuntimeInstallOperation operation,
        CancellationToken cancellationToken)
    {
        switch (operation)
        {
            case ScriptRuntimeInstallOperation scriptNodeInstallOperation:
            {
                var process = nativeInterop.CreateProcess(
                    new ProcessCreationOptions
                    {
                        Type = ProcessStartType.Bash,
                        Executable = await SearchScriptAsync(scriptNodeInstallOperation.Name.Trim(), cancellationToken),
                        Arguments = [scriptNodeInstallOperation.Args],
                        WorkingDirectory = installationFolderPath
                    });
                var result = await process.WaitForExitAsync(cancellationToken);
                break;
            }
            case BashRuntimeInstallOperation bashNodeInstallOperation:
            {
                var process = nativeInterop.CreateProcess(
                    new ProcessCreationOptions
                    {
                        Type = ProcessStartType.Bash,
                        Arguments = bashNodeInstallOperation.Command.Split(Environment.NewLine),
                        WorkingDirectory = installationFolderPath
                    });
                var result = await process.WaitForExitAsync(cancellationToken);
                break;
            }
            default:
            {
                throw new NotSupportedException($"Unsupported operation type: {operation.GetType()}");
            }
        }
    }

    public async Task InstallPackageAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var nodeMetadata = await LoadSourcePackageAsync(metadata, cancellationToken);
        // e.g. $(UserProfile)/nodis/packages/nodisai.main.ollama
        var installationFolderPath = Path.Combine(PackagesFolderPath, $"{metadata.Namespace}.{metadata.Name}".ToLower());
        Directory.CreateDirectory(installationFolderPath);
        var runtimesFolderPath = Path.Combine(installationFolderPath, "runtimes");

        foreach (var runtime in nodeMetadata.Runtimes)
        {
            Directory.CreateDirectory(runtimesFolderPath);

            foreach (var preInstall in runtime.PreInstalls)
            {
                await ExecuteInstallOperationAsync(installationFolderPath, preInstall, cancellationToken);
            }

            switch (runtime)
            {
                case GitRuntimeMetadata gitRuntimeMetadata:
                {
                    var process = nativeInterop.CreateProcess(
                        new ProcessCreationOptions
                        {
                            Type = ProcessStartType.Bash,
                            Arguments =
                            [
                                $"git clone {gitRuntimeMetadata.Url} data",
                                $"git checkout {gitRuntimeMetadata.Commit}"
                            ],
                            WorkingDirectory = installationFolderPath
                        });
                    var result = await process.WaitForExitAsync(cancellationToken);
                    if (result != 0) throw new Exception($"[{result}] Failed to clone {gitRuntimeMetadata.Url}");
                    break;
                }
                case ExecutableBundleRuntimeMetadata executableBundleRuntimeMetadata:
                {
                    // win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64
                    var operatingSystem = Environment.OSVersion.Platform switch
                    {
                        PlatformID.Win32NT => "win",
                        PlatformID.Unix => "linux",
                        PlatformID.MacOSX => "osx",
                        _ => throw new NotSupportedException($"Unsupported operating system: {Environment.OSVersion.Platform}")
                    };
                    var architecture = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => "x64",
                        Architecture.Arm64 => "arm64",
                        _ => throw new NotSupportedException($"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}")
                    };
                    if (!executableBundleRuntimeMetadata.Distributions.TryGetValue($"{operatingSystem}-{architecture}", out var platform) &&
                        !executableBundleRuntimeMetadata.Distributions.TryGetValue($"{operatingSystem}-universal", out platform))
                    {
                        throw new FileNotFoundException($"Platform {operatingSystem}-{architecture} not found.");
                    }

                    var response = await httpClient.GetAsync(platform.Url, cancellationToken);
                    await using var inputStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    var tempDownloadFileName = $"{metadata.Namespace}.{metadata.Name}.{metadata.Version}".ToLower();
                    var tempDownloadFilePath = Path.Combine(IEnvironmentManager.CacheFolderPath, tempDownloadFileName);
                    await using var outputStream = File.Create(tempDownloadFilePath, 0, FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                    {
                        using var hashAlgorithm = platform.Checksum.CreateHashAlgorithm();
                        var buffer = ArrayPool<byte>.Shared.Rent(81920);
                        try
                        {
                            int bytesRead;
                            while ((bytesRead = await inputStream.ReadAsync(new Memory<byte>(buffer), cancellationToken)) != 0)
                            {
                                await outputStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken);
                                hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }

                        hashAlgorithm.TransformFinalBlock([], 0, 0);
                        if (hashAlgorithm.Hash?.SequenceEqual(Convert.FromHexString(platform.Checksum.Value)) is not true)
                        {
                            throw new Exception("Hash mismatch.");
                        }
                    }

                    var decompressCommand = platform.Type switch
                    {
                        CompressedExecutableNodeServiceDistributionType.Zip =>
                            $"unzip -d {nativeInterop.GetFullPath(runtimesFolderPath)} {nativeInterop.GetFullPath(tempDownloadFilePath)}",
                        CompressedExecutableNodeServiceDistributionType.Tar =>
                            $"tar -xf {nativeInterop.GetFullPath(tempDownloadFilePath)} -C {nativeInterop.GetFullPath(runtimesFolderPath)}",
                        CompressedExecutableNodeServiceDistributionType.Tgz =>
                            $"tar -xzf {nativeInterop.GetFullPath(tempDownloadFilePath)} -C {nativeInterop.GetFullPath(runtimesFolderPath)}",
                        _ => throw new NotSupportedException($"Unsupported compression type: {platform.Type}")
                    };

                    var process = nativeInterop.CreateProcess(
                        new ProcessCreationOptions
                        {
                            Type = ProcessStartType.Bash,
                            Arguments = [decompressCommand]
                        });
                    var result = await process.WaitForExitAsync(cancellationToken);
                    if (result != 0) throw new Exception($"[{result}] Failed to decompress {tempDownloadFilePath}");
                    break;
                }
                default:
                {
                    throw new NotSupportedException($"Unsupported runtime type: {runtime.GetType()}");
                }
            }

            foreach (var postInstall in runtime.PostInstalls)
            {
                await ExecuteInstallOperationAsync(installationFolderPath, postInstall, cancellationToken);
            }
        }
    }

    public async Task<IAsyncDisposable> EnsureRuntimesAsync(
        string @namespace,
        IEnumerable<NameAndVersionConstraints> runtimeConstraintsList,
        CancellationToken cancellationToken)
    {
        foreach (var runtimeConstraints in runtimeConstraintsList)
        {
            var nodeMetadataPathEnumerator = string.IsNullOrEmpty(@namespace) ?
                Directory.EnumerateDirectories(PackagesFolderPath)
                    .SelectMany(Directory.EnumerateDirectories)
                    .Select(p => Path.Combine(p, runtimeConstraints.Name))
                    .Where(Directory.Exists)
                    .SelectMany(p => Directory.EnumerateFiles(p, "*.yaml")) :
                Directory.EnumerateFiles(
                    Path.Combine(PackagesFolderPath, @namespace.Replace('.', Path.DirectorySeparatorChar), runtimeConstraints.Name),
                    "*.yaml");
            foreach (var nodeMetadataPath in nodeMetadataPathEnumerator)
            {
                if (!SemanticVersion.TryParse(Path.GetFileNameWithoutExtension(nodeMetadataPath), out var version)) continue;
                if (!runtimeConstraints.IsSatisfied(version)) continue;
                await using var fs = File.OpenRead(nodeMetadataPath);
                var nodeMetadata = await YamlSerializer.DeserializeAsync<PackageMetadata>(fs, yamlSerializerOptions);
                _ = nativeInterop.CreateProcess(
                    new ProcessCreationOptions
                    {
                        Arguments = [nodeMetadata.Runtimes[0].To<ExecutableBundleRuntimeMetadata>().Distributions["win-x64"].Execution.Command],
                        WorkingDirectory = Path.ChangeExtension(nodeMetadataPath, null)
                    }).WaitForExitAsync(cancellationToken);
                await Task.Delay(3000, cancellationToken);
                return null!;
            }
        }

        return null!;
    }

    private readonly record struct RuntimeMetadata(string Namespace, string Name, SemanticVersion Version, string Id);
}

/// <summary>
/// Contains all installed packages.
/// </summary>
/// <remarks>
/// $(UserProfile)/nodis/sources/index.yaml
/// </remarks>
[YamlObject]
public partial record SourceIndexEntry(string Url, string Namespace)
{
    public static SourceIndexEntry Main => new("https://github.com/NodisAI/Main", "NodisAI.Main");
}

/// <summary>
/// Contains all installed packages.
/// </summary>
/// <remarks>
/// $(UserProfile)/nodis/packages/index.yaml
/// </remarks>
[YamlObject]
public partial record PackagesIndexEntry();