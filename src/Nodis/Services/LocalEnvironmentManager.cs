using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using Nodis.Extensions;
using Nodis.Interfaces;
using Nodis.Models;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Services;

public class LocalEnvironmentManager(
    INativeInterop nativeInterop,
    YamlSerializerOptions yamlSerializerOptions
) : IEnvironmentManager
{
    private static string SourcesFolderPath => Path.Combine(IEnvironmentManager.DataFolderPath, "Sources");
    private static string NodesFolderPath => Path.Combine(IEnvironmentManager.DataFolderPath, "Nodes");

    public IEnumerable<Metadata> EnumerateSources()
    {
        if (!Directory.Exists(SourcesFolderPath)) yield break;
        foreach (var metadataFilePath in Directory
                     .EnumerateDirectories(SourcesFolderPath)
                     .SelectMany(Directory.EnumerateDirectories)
                     .Select(p => Path.Combine(p, "Packages"))
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

    public IEnumerable<Metadata> EnumerateNodes()
    {
        if (!Directory.Exists(NodesFolderPath)) yield break;
        foreach (var nodeFolderPath in Directory
                     .EnumerateDirectories(NodesFolderPath)
                     .SelectMany(Directory.EnumerateDirectories)
                     .SelectMany(Directory.EnumerateDirectories)
                     .SelectMany(Directory.EnumerateDirectories))
        {
            if (!SemanticVersion.TryParse(Path.GetFileName(nodeFolderPath), out var version)) continue;
            var folderParts = nodeFolderPath.Split(Path.DirectorySeparatorChar);
            yield return new Metadata($"{folderParts[^4]}.{folderParts[^3]}", folderParts[^2], version);
        }
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

    public async Task UpdateSourcesAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(SourcesFolderPath);
        foreach (var (url, @namespace) in await LoadSourceIndexEntriesAsync(cancellationToken))
        {
            var sourceFolderPath = Path.Combine(SourcesFolderPath, @namespace.Replace('.', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(sourceFolderPath);

            var execution = nativeInterop.BashExecute(
                new BashExecutionOptions
                {
                    CommandLines = ["git pull"],
                    WorkingDirectory = sourceFolderPath
                });
            var result = await execution.WaitAsync(cancellationToken);
            // 128 means the folder is not a git repository
            // see https://stackoverflow.com/questions/4917871/does-git-return-specific-return-error-codes
            if (result == 128)
            {
                Directory.Delete(sourceFolderPath, true);
                execution = nativeInterop.BashExecute(
                    new BashExecutionOptions
                    {
                        CommandLines = [$"git clone {url} {Path.GetFullPath(sourceFolderPath)}"],
                        WorkingDirectory = SourcesFolderPath
                    });
                result = await execution.WaitAsync(cancellationToken);

                if (result != 0) throw new Exception($"Failed to clone {url}\n{await execution.StandardError.ReadToEndAsync(cancellationToken)}");
            }
        }
    }

    public async Task<NodeMetadata> LoadNodeAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var nodeMetadataPath = Path.Combine(
            NodesFolderPath,
            metadata.Namespace.Replace('.', Path.DirectorySeparatorChar),
            metadata.Name,
            metadata.Version + ".yaml");
        await using var fs = File.OpenRead(nodeMetadataPath);
        return await YamlSerializer.DeserializeAsync<NodeMetadata>(fs, yamlSerializerOptions);
    }

    private async static ValueTask<string> SearchScriptAsync(string scriptName, CancellationToken cancellationToken)
    {
        var indexEntries = await LoadSourceIndexEntriesAsync(cancellationToken);
        foreach (var entry in indexEntries)
        {
            var scriptPath = Path.Combine(
                SourcesFolderPath,
                entry.Namespace.Replace('.', Path.DirectorySeparatorChar),
                "Scripts",
                scriptName + ".sh");
            if (File.Exists(scriptPath)) return scriptPath;
        }

        throw new FileNotFoundException($"Script {scriptName} not found.");
    }

    private async ValueTask ExecuteInstallOperationAsync(
        string installationFolderPath,
        NodeInstallOperation operation,
        CancellationToken cancellationToken)
    {
        switch (operation)
        {
            case ScriptNodeInstallOperation scriptNodeInstallOperation:
            {
                var execution = nativeInterop.BashExecute(
                    new BashExecutionOptions
                    {
                        ScriptPath = await SearchScriptAsync(scriptNodeInstallOperation.Name.Trim(), cancellationToken),
                        CommandLines = [scriptNodeInstallOperation.Args],
                        WorkingDirectory = installationFolderPath
                    });
                var result = await execution.WaitAsync(cancellationToken);
                break;
            }
            case BashNodeInstallOperation bashNodeInstallOperation:
            {
                var execution = nativeInterop.BashExecute(
                    new BashExecutionOptions
                    {
                        CommandLines = bashNodeInstallOperation.Command.Split(Environment.NewLine),
                        WorkingDirectory = installationFolderPath
                    });
                var result = await execution.WaitAsync(cancellationToken);
                break;
            }
            default:
            {
                throw new NotSupportedException($"Unsupported operation type: {operation.GetType()}");
            }
        }
    }

    public async Task InstallNodeAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var nodeMetadata = await LoadNodeAsync(metadata, cancellationToken);
        var installationFolderPath = Path.Combine(
            NodesFolderPath,
            metadata.Namespace.Replace('.', Path.DirectorySeparatorChar),
            metadata.Name,
            metadata.Version.ToString());
        Directory.CreateDirectory(installationFolderPath);

        foreach (var preInstall in nodeMetadata.PreInstall)
        {
            await ExecuteInstallOperationAsync(Path.Combine(installationFolderPath, "runtimes"), preInstall, cancellationToken);
        }

        foreach (var runtime in nodeMetadata.Runtimes)
        {
            switch (runtime)
            {
                case GitNodeRuntime gitNodeSource:
                {
                    var execution = nativeInterop.BashExecute(
                        new BashExecutionOptions
                        {
                            CommandLines =
                            [
                                $"git clone {gitNodeSource.Url} data",
                                $"git checkout {gitNodeSource.Commit}"
                            ],
                            WorkingDirectory = installationFolderPath
                        });
                    var result = await execution.WaitAsync(cancellationToken);
                    break;
                }
                case ExecutableBundleNodeRuntime compressedExecutableNodeSource:
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
                    if (!compressedExecutableNodeSource.Distributions.TryGetValue($"{operatingSystem}-{architecture}", out var platform) &&
                        !compressedExecutableNodeSource.Distributions.TryGetValue($"{operatingSystem}-universal", out platform))
                    {
                        throw new FileNotFoundException($"Platform {operatingSystem}-{architecture} not found.");
                    }

                    var response = await App.Resolve<HttpClient>().GetAsync(platform.Url, cancellationToken);
                    await using var inputStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var outputStream = File.Create(Path.Combine(installationFolderPath, ".download"));
                    await inputStream.CopyToAsync(outputStream, cancellationToken);

                    outputStream.Seek(0, SeekOrigin.Begin);
                    using var sha256 = SHA256.Create();
                    var hash = await sha256.ComputeHashAsync(outputStream, cancellationToken);
                    if (!hash.SequenceEqual(Convert.FromHexString(platform.Checksum)))
                    {
                        throw new Exception("SHA256 mismatch.");
                    }

                    switch (platform.Type)
                    {
                        case CompressedExecutableNodeServiceDistributionType.Zip:
                        {
                            // Tell me, Microsoft, why this doesn't support async?
                            using var archive = new ZipArchive(outputStream);
                            archive.ExtractToDirectory(installationFolderPath);
                            break;
                        }
                        case CompressedExecutableNodeServiceDistributionType.Tgz:
                        {
                            await using var archiveStream = new GZipStream(outputStream, CompressionMode.Decompress);
                            await using var reader = new TarReader(archiveStream);
                            while (await reader.GetNextEntryAsync(cancellationToken: cancellationToken) is { } entry)
                            {
                                var entryPath = Path.Combine(installationFolderPath, entry.Name);
                                switch (entry.EntryType)
                                {
                                    case TarEntryType.RegularFile:
                                        await entry.ExtractToFileAsync(entryPath, true, cancellationToken);
                                        break;
                                    case TarEntryType.Directory:
                                        Directory.CreateDirectory(entryPath);
                                        break;
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
                default:
                {
                    throw new NotSupportedException($"Unsupported source type: {runtime.GetType()}");
                }
            }
        }

        foreach (var postInstall in nodeMetadata.PostInstall)
        {
            await ExecuteInstallOperationAsync(Path.Combine(installationFolderPath, "data"), postInstall, cancellationToken);
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
                Directory.EnumerateDirectories(NodesFolderPath)
                    .SelectMany(Directory.EnumerateDirectories)
                    .Select(p => Path.Combine(p, runtimeConstraints.Name))
                    .Where(Directory.Exists)
                    .SelectMany(p => Directory.EnumerateFiles(p, "*.yaml")) :
                Directory.EnumerateFiles(
                    Path.Combine(NodesFolderPath, @namespace.Replace('.', Path.DirectorySeparatorChar), runtimeConstraints.Name),
                    "*.yaml");
            foreach (var nodeMetadataPath in nodeMetadataPathEnumerator)
            {
                if (!SemanticVersion.TryParse(Path.GetFileNameWithoutExtension(nodeMetadataPath), out var version)) continue;
                if (!runtimeConstraints.IsSatisfied(version)) continue;
                await using var fs = File.OpenRead(nodeMetadataPath);
                var nodeMetadata = await YamlSerializer.DeserializeAsync<NodeMetadata>(fs, yamlSerializerOptions);
                _ = nativeInterop.BashExecute(new BashExecutionOptions
                {
                    CommandLines = [nodeMetadata.Runtimes[0].To<ExecutableBundleNodeRuntime>().Distributions["win-x64"].Execution.Command],
                    WorkingDirectory = Path.ChangeExtension(nodeMetadataPath, null)
                }).WaitAsync(cancellationToken);
                await Task.Delay(3000, cancellationToken);
                return null!;
            }
        }

        return null!;
    }

    private readonly record struct RuntimeMetadata(string Namespace, string Name, SemanticVersion Version, string Id);
}

[YamlObject]
public partial record SourceIndexEntry(string Url, string Namespace)
{
    public static SourceIndexEntry Main => new("https://github.com/NodisAI/Main", "NodisAI.Main");
}