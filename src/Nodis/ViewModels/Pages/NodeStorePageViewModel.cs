using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    internal partial NodeWrapper? SelectedNode { get; set; }

    private async IAsyncEnumerable<NodeWrapper> LoadSourcesAsync()
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
        }

        foreach (var (url, relativePath) in entries)
        {
            var sourceFolderPath = Path.Combine(SourcesFolderPath, relativePath);
            Directory.CreateDirectory(sourceFolderPath);

            var result = await bashExecutor.Execute(
                new BashExecutionOptions
                {
                    CommandLines = ["git pull --quiet"],
                    WorkingDirectory = sourceFolderPath
                }).WaitAsync();

            if (result != 0)
            {

                Directory.Delete(sourceFolderPath, true);
                var execution = bashExecutor.Execute(
                    new BashExecutionOptions
                    {
                        CommandLines = [$"git clone {url} {relativePath}"],
                        WorkingDirectory = SourcesFolderPath
                    });
                result = await execution.WaitAsync();

                if (result != 0) throw new Exception($"Failed to clone {url}");
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

    internal class NodeWrapper(NodeMetadata node, List<Version> versions)
    {
        public NodeMetadata Node { get; } = node;

        public List<Version> Versions { get; } = versions;

        public AsyncProperty<string?> ReadmeMarkdown { get; } = new(
            async () =>
            {
                if (!Uri.TryCreate(node.Readme, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not "http" and not "https") return null;

                var response = await App.Resolve<HttpClient>().GetAsync(uri);
                return await response.Content.ReadAsStringAsync();
            });
    }
}