using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nodis.Extensions;
using Nodis.Interfaces;
using Nodis.Models;
using VYaml.Serialization;

namespace Nodis.ViewModels;

public partial class NodeStorePageViewModel(
    IBashExecutor bashExecutor)
    : ReactiveViewModelBase
{
    private static string DataFolderPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(Nodis));
    private static string NodeFolderPath { get; } = Path.Combine(DataFolderPath, "Node");
    private static string SourcesFolderPath { get; } = Path.Combine(DataFolderPath, "Sources");

    internal ObservableCollection<NodeWrapper> Nodes { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanDownloadNode))]
    internal partial NodeWrapper? SelectedNode { get; set; }

    public bool CanDownloadNode => SelectedNode is not null;

    private async static ValueTask<IReadOnlyList<SourceIndexEntry>> LoadSourceIndexEntriesAsync()
    {
        Directory.CreateDirectory(SourcesFolderPath);
        var indexJsonPath = Path.Combine(SourcesFolderPath, "index.json");
        IReadOnlyList<SourceIndexEntry> entries;
        try
        {
            entries = await JsonSerializer.DeserializeAsync<IReadOnlyList<SourceIndexEntry>>(File.OpenRead(indexJsonPath)).NotNullAsync();
        }
        catch
        {
            entries = [SourceIndexEntry.Main];
            await JsonSerializer.SerializeAsync(File.Create(indexJsonPath), entries);
        }

        return entries;
    }

    private async static ValueTask<string> SearchScriptAsync(string scriptName)
    {
        var indexEntries = await LoadSourceIndexEntriesAsync();
        foreach (var entry in indexEntries)
        {
            var scriptPath = Path.Combine(SourcesFolderPath, entry.RelativePath, "Scripts", scriptName + ".sh");
            if (File.Exists(scriptPath)) return scriptPath;
        }

        throw new FileNotFoundException($"Script {scriptName} not found.");
    }

    [RelayCommand]
    private async Task DownloadNodeAsync()
    {
        if (SelectedNode is not { } selectedNode) return;

        var installationFolderPath = Path.Combine(NodeFolderPath, selectedNode.Title, selectedNode.SelectedVersion.ToString());
        Directory.CreateDirectory(installationFolderPath);

        foreach (var preInstall in selectedNode.Metadata.PreInstall)
        {
            await ExecuteInstallOperationAsync(Path.Combine(installationFolderPath, "runtimes"), preInstall);
        }

        switch (selectedNode.Metadata.Source.Type)
        {
            case NodeSourceType.Git:
            {
                if (selectedNode.Metadata.Source.Commit == null) throw new ArgumentNullException(nameof(NodeSource.Commit));
                var execution = bashExecutor.Execute(
                    new BashExecutionOptions
                    {
                        CommandLines =
                        [
                            $"git clone {selectedNode.Metadata.Source.Url} data",
                            $"git checkout {selectedNode.Metadata.Source.Commit}"
                        ],
                        WorkingDirectory = installationFolderPath
                    });
                var result = await execution.WaitAsync();
                break;
            }
        }

        foreach (var postInstall in selectedNode.Metadata.PostInstall)
        {
            await ExecuteInstallOperationAsync(Path.Combine(installationFolderPath, "data"), postInstall);
        }
    }

    private async Task ExecuteInstallOperationAsync(string installationFolderPath, NodeInstallOperation operation)
    {
        switch (operation.Type)
        {
            case NodeInstallOperationType.Script:
            {
                if (operation.Name.IsNullOrEmpty()) throw new ArgumentException(nameof(NodeInstallOperation.Name));
                var execution = bashExecutor.Execute(
                    new BashExecutionOptions
                    {
                        ScriptPath = await SearchScriptAsync(operation.Name.Trim()),
                        CommandLines = [operation.Args],
                        WorkingDirectory = installationFolderPath
                    });
                var result = await execution.WaitAsync();
                break;
            }
            case NodeInstallOperationType.Bash:
            {
                var execution = bashExecutor.Execute(
                    new BashExecutionOptions
                    {
                        CommandLines = operation.Args.Split(Environment.NewLine),
                        WorkingDirectory = installationFolderPath
                    });
                var result = await execution.WaitAsync();
                break;
            }
            default:
            {
                throw new NotSupportedException($"Unsupported operation type: {operation.Type}");
            }
        }
    }

    private async IAsyncEnumerable<NodeWrapper> LoadSourcesAsync()
    {
        foreach (var (url, relativePath) in await LoadSourceIndexEntriesAsync())
        {
            var sourceFolderPath = Path.Combine(SourcesFolderPath, relativePath);
            Directory.CreateDirectory(sourceFolderPath);

            var execution = bashExecutor.Execute(
                new BashExecutionOptions
                {
                    CommandLines = ["git pull"],
                    WorkingDirectory = sourceFolderPath
                });
            var result = await execution.WaitAsync();
            if (result != 0)
            {
                Directory.Delete(sourceFolderPath, true);
                execution = bashExecutor.Execute(
                    new BashExecutionOptions
                    {
                        CommandLines = [$"git clone {url} {relativePath}"],
                        WorkingDirectory = SourcesFolderPath
                    });
                result = await execution.WaitAsync();

                if (result != 0) throw new Exception($"Failed to clone {url}\n{await execution.StandardError.ReadToEndAsync()}");
            }

            foreach (var packageFolderPath in Directory.EnumerateDirectories(Path.Combine(sourceFolderPath, "Packages")))
            {
                var metadataFilePaths = Directory
                    .EnumerateFiles(packageFolderPath, "*.yaml")
                    .Select(
                        p => new
                        {
                            Path = p,
                            Version = Version.Parse(Path.GetFileNameWithoutExtension(p))
                        })
                    .OrderBy(p => p.Version)
                    .ToList();
                if (metadataFilePaths.Count == 0) continue;
                yield return new NodeWrapper(
                    Path.GetFileName(packageFolderPath),
                    await YamlSerializer.DeserializeAsync<NodeMetadata>(File.OpenRead(metadataFilePaths[0].Path)),
                    metadataFilePaths.Select(p => p.Version).ToList());
            }
        }
    }

    protected internal override async Task ViewLoaded()
    {
        await foreach (var node in LoadSourcesAsync())
        {
            Nodes.Add(node);
        }

        await base.ViewLoaded();
    }

    private record SourceIndexEntry(string Url, string RelativePath)
    {
        public static SourceIndexEntry Main => new("https://github.com/NodisAI/Main", "NodisAI/Main");
    }

    internal partial class NodeWrapper(string title, NodeMetadata metadata, List<Version> versions) : ObservableObject
    {
        public string Title { get; } = title;

        public NodeMetadata Metadata { get; } = metadata;

        public List<Version> Versions { get; } = versions;

        [ObservableProperty]
        public partial Version SelectedVersion { get; set; } = versions[0];

        public AsyncProperty<string?> ReadmeMarkdown { get; } = new(
            async () =>
            {
                if (!Uri.TryCreate(metadata.Readme, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not "http" and not "https") return null;

                var response = await App.Resolve<HttpClient>().GetAsync(uri);
                return await response.Content.ReadAsStringAsync();
            });
    }
}