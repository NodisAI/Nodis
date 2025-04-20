using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.KernelMemory;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using Nodis.Backend.Interfaces;
using Nodis.Backend.Models.Mcp;
using Nodis.Core.Extensions;
using Nodis.Core.Interfaces;
using Nodis.Core.Models;
using Nodis.Core.Models.Workflow;
using VYaml.Serialization;

namespace Nodis.Backend.Services;

public class LocalEnvironmentManager(
    IHttpClientFactory httpClientFactory,
    INativeInterop nativeInterop,
    IKernelMemory kernelMemory,
    YamlSerializerOptions yamlSerializerOptions,
    ILoggerFactory loggerFactory
) : IEnvironmentManager
{
    private const string SourcesFolderName = "sources";
    private const string BundlesFolderName = "bundles";
    private const string InstalledBundleMetadataFileName = "metadata.yaml";

    private static string SourcesFolderPath => Path.Combine(IEnvironmentManager.DataFolderPath, SourcesFolderName);
    private static string BundlesFolderPath => Path.Combine(IEnvironmentManager.DataFolderPath, BundlesFolderName);

    private readonly HttpClient httpClient = httpClientFactory.CreateClient("global");
    private readonly ConcurrentDictionary<Metadata, IProcess> runningRuntimes = new();

    public Task<IEnumerable<Metadata>> EnumerateBundleManifestMetadataAsync() => Task.FromResult(EnumerateBundleManifestMetadata());

    private static IEnumerable<Metadata> EnumerateBundleManifestMetadata()
    {
        // e.g. $(UserProfile)/nodis/sources/NodesAI/Main/bundles/notion/1.0.0.yaml
        if (!Directory.Exists(SourcesFolderPath)) yield break;
        foreach (var manifestFilePath in Directory
                     .EnumerateDirectories(SourcesFolderPath) // $(UserProfile)/nodis/sources/NodesAI
                     .SelectMany(Directory.EnumerateDirectories) // $(UserProfile)/nodis/sources/NodesAI/Main
                     .Select(p => Path.Combine(p, BundlesFolderName)) // $(UserProfile)/nodis/sources/NodesAI/Main/bundles
                     .Where(Directory.Exists)
                     .SelectMany(Directory.EnumerateDirectories) // $(UserProfile)/nodis/sources/NodesAI/Main/bundles/notion
                     .SelectMany(Directory.EnumerateFiles))
        {
            if (!SemanticVersion.TryParse(Path.GetFileNameWithoutExtension(manifestFilePath), out var version)) continue;
            var folderParts = manifestFilePath.Split(Path.DirectorySeparatorChar);
            yield return new Metadata($"{folderParts[^5]}.{folderParts[^4]}", folderParts[^2], version);
        }
    }

    public Task<IEnumerable<Metadata>> EnumerateInstalledBundleMetadataAsync() => Task.FromResult(EnumerateInstalledBundleMetadata());

    private static IEnumerable<Metadata> EnumerateInstalledBundleMetadata()
    {
        // e.g. $(UserProfile)/nodis/bundles/nodisai.main/notion/1.0.0/metadata.yaml
        if (!Directory.Exists(BundlesFolderPath)) yield break;
        foreach (var installedBundlePath in Directory
                     .EnumerateDirectories(BundlesFolderPath) // $(UserProfile)/nodis/bundles/nodisai.main
                     .SelectMany(Directory.EnumerateDirectories) // $(UserProfile)/nodis/bundles/nodisai.main/notion
                     .SelectMany(Directory.EnumerateDirectories) // $(UserProfile)/nodis/bundles/nodisai.main/notionn/1.0.0
                     .Where(p => new FileInfo(Path.Combine(p, InstalledBundleMetadataFileName)) is { Exists: true, Length: > 0 }))
        {
            if (!SemanticVersion.TryParse(Path.GetFileNameWithoutExtension(installedBundlePath), out var version)) continue;
            var folderParts = installedBundlePath.Split(Path.DirectorySeparatorChar);
            yield return new Metadata(folderParts[^3], folderParts[^2], version);
        }
    }

    private async static ValueTask<IReadOnlyList<SourceListEntry>> LoadSourceListEntriesAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(SourcesFolderPath);
        var indexJsonPath = Path.Combine(SourcesFolderPath, "sources.yaml");
        IReadOnlyList<SourceListEntry> entries;
        try
        {
            await using var fs = File.OpenRead(indexJsonPath);
            var buffer = new byte[fs.Length];
            if (await fs.ReadAsync(buffer, cancellationToken) != buffer.Length) throw new IOException();
            entries = YamlSerializer.Deserialize<IReadOnlyList<SourceListEntry>>(buffer).NotNull();
        }
        catch
        {
            entries = [SourceListEntry.Main];
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
        foreach (var (url, @namespace) in await LoadSourceListEntriesAsync(cancellationToken))
        {
            var relativePath = @namespace.Replace('.', Path.DirectorySeparatorChar);
            var sourceFolderPath = Path.Combine(SourcesFolderPath, relativePath);
            Directory.CreateDirectory(sourceFolderPath);

            var process = nativeInterop.CreateProcess(
                new BashProcessCreationOptions
                {
                    CommandLines = ["git pull"],
                    WorkingDirectory = sourceFolderPath
                });
            var result = await process.WaitForExitAsync(cancellationToken);
            // 128 means the folder is not a git repository
            // see https://stackoverflow.com/questions/4917871/does-git-return-specific-return-error-codes
            if (result == 128)
            {
                RecursivelyDeleteDirectory(sourceFolderPath);
                process = nativeInterop.CreateProcess(
                    new BashProcessCreationOptions
                    {
                        CommandLines = [$"git clone {url} {relativePath.Replace('\\', '/')}"],
                        WorkingDirectory = SourcesFolderPath
                    });
                result = await process.WaitForExitAsync(cancellationToken);
                if (result != 0) throw new Exception($"Failed to clone {url}\n{await process.StandardError.ReadToEndAsync(cancellationToken)}");
            }
        }

        // await BuildSourcesKernelMemoryAsync(cancellationToken);
//
        // var searchResult = await kernelMemory.SearchAsync(
        //     "search web pages",
        //     cancellationToken: cancellationToken);
    }

    private Task BuildSourcesKernelMemoryAsync(CancellationToken cancellationToken) => Parallel.ForEachAsync(
        EnumerateBundleManifestMetadata(),
        new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = 8
        },
        async (metadata, token) =>
        {
            var bundleManifest = await LoadBundleManifestAsync(metadata, cancellationToken);
            await kernelMemory.ImportTextAsync(
                bundleManifest.Description,
                tags:
                new TagCollection
                {
                    { "namespace", metadata.Namespace },
                    { "name", metadata.Name },
                    { "version", metadata.Version.ToString() }
                },
                cancellationToken: token);
        });

    /// <summary>
    /// recursively delete a directory, with FileAttributes set to Normal.
    /// This can sometimes be necessary to delete a directory that contains read-only files.
    /// </summary>
    /// <param name="target"></param>
    private static void RecursivelyDeleteDirectory(string target)
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

    public async Task<BundleManifest> LoadBundleManifestAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var bundleManifestPath = Path.Combine(
            SourcesFolderPath,
            metadata.Namespace.Replace('.', Path.DirectorySeparatorChar),
            BundlesFolderName,
            metadata.Name,
            metadata.Version + ".yaml");
        await using var fs = File.OpenRead(bundleManifestPath);
        return await YamlSerializer.DeserializeAsync<BundleManifest>(fs, yamlSerializerOptions);
    }

    public async Task<InstalledBundle> LoadInstalledBundleAsync(Metadata metadata, CancellationToken cancellationToken)
    {
        var bundleManifestPath = Path.Combine(
            BundlesFolderPath,
            metadata.Namespace.ToLower(),
            metadata.Name,
            metadata.Version.ToString(),
            InstalledBundleMetadataFileName);
        await using var fs = File.OpenRead(bundleManifestPath);
        return await YamlSerializer.DeserializeAsync<InstalledBundle>(fs, yamlSerializerOptions);
    }

    private async ValueTask ExecuteInstallOperationAsync(
        string runtimeFolderPath,
        RuntimeInstallOperation operation,
        IAdvancedProgress? progress,
        CancellationToken cancellationToken)
    {
        switch (operation)
        {
            case BashRuntimeInstallOperation bashRuntimeInstallOperation:
            {
                progress?.Report(string.Join('\n', bashRuntimeInstallOperation.CommandLines));
                var process = nativeInterop.CreateProcess(
                    new BashProcessCreationOptions
                    {
                        CommandLines = bashRuntimeInstallOperation.CommandLines,
                        WorkingDirectory = runtimeFolderPath
                    });
                await process.WaitForExitAsync(cancellationToken);
                break;
            }
            case GitRuntimeInstallOperation gitRuntimeInstallOperation:
            {
                var process = nativeInterop.CreateProcess(
                    new BashProcessCreationOptions
                    {
                        CommandLines = [$"git clone {gitRuntimeInstallOperation.Url}"],
                        WorkingDirectory = runtimeFolderPath
                    });
                await process.WaitForExitAsync(cancellationToken);
                break;
            }
            default:
            {
                throw new NotSupportedException($"Unsupported operation type: {operation.GetType()}");
            }
        }
    }

    public async Task<InstalledBundle> InstallBundleAsync(
        Metadata metadata,
        IAdvancedProgress? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(0d);
        progress?.Report("Reading bundle manifest...");

        // e.g. $(UserProfile)/nodis/bundles/nodisai.main/notion/1.0.0
        var installationFolderPath = Path.Combine(
            BundlesFolderPath,
            metadata.Namespace.ToLower(),
            metadata.Name.ToLower(),
            metadata.Version.ToString());
        var installedBundleMetadataPath = Path.Combine(installationFolderPath, InstalledBundleMetadataFileName);
        var fileInfo = new FileInfo(installedBundleMetadataPath);
        if (fileInfo is { Exists: true, Length: > 0 } && await TryReadInstalledBundle() is { } installedBundle) return installedBundle;
        var bundleManifest = await LoadBundleManifestAsync(metadata, cancellationToken);

        // install runtime (10% - 99%)
        progress?.Report(10d);
        progress?.Report("Installing runtimes...");
        Directory.CreateDirectory(installationFolderPath);
        var progressPerRuntime = 89d / bundleManifest.Runtimes.Count;
        var runtimesFolderPath = Path.Combine(installationFolderPath, "runtimes");
        var bundleNodes = new List<BundleNode>();
        foreach (var runtime in bundleManifest.Runtimes)
        {
            var runtimeFolderPath = Path.Combine(runtimesFolderPath, runtime.Id);
            Directory.CreateDirectory(runtimeFolderPath);

            var progressPerStep = progressPerRuntime / (runtime.PreInstalls?.Count ?? 0d + runtime.PostInstalls?.Count ?? 0d + 1);

            // todo: BundleRuntimeType
            if (runtime.PreInstalls != null)
            {
                foreach (var preInstall in runtime.PreInstalls)
                {
                    await ExecuteInstallOperationAsync(runtimeFolderPath, preInstall, progress, cancellationToken);
                    progress?.Advance(progressPerStep);
                }
            }

            switch (runtime)
            {
                case McpBundleRuntimeConfiguration mcp:
                {
                    await foreach (var node in ParseMcpBundleNodesAsync(
                                       metadata,
                                       runtime.Id,
                                       mcp,
                                       runtimeFolderPath,
                                       progressPerStep,
                                       progress,
                                       cancellationToken))
                    {
                        bundleNodes.Add(node);
                    }
                    break;
                }
            }
            progress?.Advance(progressPerStep);

            if (runtime.PostInstalls != null)
            {
                foreach (var postInstall in runtime.PostInstalls)
                {
                    await ExecuteInstallOperationAsync(runtimeFolderPath, postInstall, progress, cancellationToken);
                    progress?.Advance(progressPerStep);
                }
            }
        }

        progress?.Report(99d);
        progress?.Report("Saving changes...");
        installedBundle = new InstalledBundle(bundleManifest, bundleNodes);
        await using var outputStream = fileInfo.Create();
        await outputStream.WriteAsync(YamlSerializer.Serialize(installedBundle), cancellationToken);
        return installedBundle;

        async ValueTask<InstalledBundle?> TryReadInstalledBundle()
        {
            try
            {
                await using var inputStream = fileInfo.OpenRead();
                return await YamlSerializer.DeserializeAsync<InstalledBundle>(inputStream, yamlSerializerOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    private async IAsyncEnumerable<BundleMcpNode> ParseMcpBundleNodesAsync(
        Metadata metadata,
        string runtimeId,
        McpBundleRuntimeConfiguration mcp,
        string runtimeFolderPath,
        double totalProgress,
        IAdvancedProgress? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IClientTransport? clientTransport;
        switch (mcp.TransportConfiguration)
        {
            case StdioMcpTransportConfiguration stdio:
            {
                Dictionary<string, string>? environmentVariables = null;
                if (stdio.EnvironmentVariables != null)
                {
                    environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, value) in stdio.EnvironmentVariables)
                    {
                        if (value.Value != null) environmentVariables[key] = value.Value;
                    }
                }
                clientTransport = new NativeInteropClientTransport(
                    nativeInterop,
                    new NativeInteropClientTransportOptions
                    {
                        Command = stdio.Command,
                        Arguments = stdio.Arguments,
                        WorkingDirectory = Path.Combine(runtimeFolderPath, stdio.WorkingDirectory ?? string.Empty),
                        EnvironmentVariables = environmentVariables
                    },
                    loggerFactory);
                break;
            }
            case SseMcpTransportConfiguration sse:
            {
                Dictionary<string, string>? additionalHeaders = null;
                if (sse.Headers != null)
                {
                    additionalHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, value) in sse.Headers)
                    {
                        if (value.Value != null) additionalHeaders[key] = value.Value;
                    }
                }
                clientTransport = new SseClientTransport(
                    new SseClientTransportOptions
                    {
                        Endpoint = new Uri(sse.Url, UriKind.Absolute),
                        AdditionalHeaders = additionalHeaders
                    },
                    loggerFactory);
                break;
            }
            default:
            {
                throw new NotSupportedException($"Unsupported transport type: {mcp.TransportConfiguration.GetType()}");
            }
        }

        await using var client = await McpClientFactory.CreateAsync(clientTransport, cancellationToken: cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        foreach (var tool in tools)
        {
            var name = (tool.AdditionalProperties.TryGetValue("title", out var titleValue) ? titleValue?.ToString() : null) ?? tool.Name;
            var dataInputPins = tool.JsonSchema.GetProperty("properties")
                .EnumerateObject()
                .Select(
                    property => new NodeDataInputPin(
                        property.Name,
                        property.Value.GetProperty("type").GetString() switch
                        {
                            "string" => new NodeStringData(string.Empty),
                            "number" => new NodeInt64Data(0L),
                            "object" => new NodeDictionaryData(new Hashtable()),
                            "array" => new NodeEnumerableData(new ArrayList()),
                            "boolean" => new NodeBooleanData(false),
                            _ => new NodeAnyData(),
                        })
                    {
                        Description = TryGetPropertyAsString(property.Value, "description")
                    })
                .ToReadOnlyList();
            yield return new BundleMcpNode
            {
                Metadata = metadata,
                RuntimeId = runtimeId,
                Name = name,
                ToolName = tool.Name,
                Description = tool.Description,
                SerializableDataInputs = dataInputPins,
                SerializableDataOutputs = [new NodeDataOutputPin("output", new NodeAnyData())]
            };
            progress?.Advance(totalProgress / tools.Count);
        }

        static string? TryGetPropertyAsString(in JsonElement element, string key)
        {
            if (!element.TryGetProperty(key, out var value)) return null;
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            return null;
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
                Directory.EnumerateDirectories(BundlesFolderPath)
                    .SelectMany(Directory.EnumerateDirectories)
                    .Select(p => Path.Combine(p, runtimeConstraints.Name))
                    .Where(Directory.Exists)
                    .SelectMany(p => Directory.EnumerateFiles(p, "*.yaml")) :
                Directory.EnumerateFiles(
                    Path.Combine(BundlesFolderPath, @namespace.Replace('.', Path.DirectorySeparatorChar), runtimeConstraints.Name),
                    "*.yaml");
            foreach (var nodeMetadataPath in nodeMetadataPathEnumerator)
            {
                if (!SemanticVersion.TryParse(Path.GetFileNameWithoutExtension(nodeMetadataPath), out var version)) continue;
                if (!runtimeConstraints.IsSatisfied(version)) continue;
                await using var fs = File.OpenRead(nodeMetadataPath);
                var nodeMetadata = await YamlSerializer.DeserializeAsync<BundleManifest>(fs, yamlSerializerOptions);

                // _ = nativeInterop.CreateProcess(
                //     new ProcessCreationOptions
                //     {
                //         Arguments = [nodeMetadata.Runtimes[0].To<ExecutableBundleRuntimeMetadata>().Distributions["win-x64"].Execution.Command],
                //         WorkingDirectory = Path.ChangeExtension(nodeMetadataPath, null)
                //     }).WaitForExitAsync(cancellationToken);
                // await Task.Delay(3000, cancellationToken);
                // return null!;
            }
        }

        throw new NotImplementedException();
    }
}