using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nodis.Interfaces;
using Nodis.Models;
using ObservableCollections;

namespace Nodis.ViewModels;

public partial class NodeStorePageViewModel(IEnvironmentManager environmentManager) : ReactiveViewModelBase
{
    [field: AllowNull, MaybeNull]
    internal NotifyCollectionChangedSynchronizedViewList<NodeWrapper> Nodes =>
        field ??= nodes.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<NodeWrapper> nodes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallNode))]
    [NotifyCanExecuteChangedFor(nameof(InstallNodeCommand))]
    internal partial NodeWrapper? SelectedNode { get; set; }

    public bool CanInstallNode => SelectedNode is not null;

    [RelayCommand]
    private Task InstallNodeAsync()
    {
        if (SelectedNode is not { } selectedNode) return Task.CompletedTask;
        return environmentManager.InstallNodeAsync(selectedNode.Metadata, CancellationToken.None);
    }

    private async IAsyncEnumerable<NodeWrapper> LoadSourcesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var group in environmentManager.EnumerateSources().GroupBy(m => $"{m.Namespace}:{m.Name}"))
        {
            var items = group.ToList();
            yield return new NodeWrapper(
                items[0].Name,
                items[0],
                await environmentManager.LoadNodeAsync(items[0], cancellationToken),
                items.Select(p => p.Version).ToList());
        }
    }

    protected internal override async Task ViewLoaded(CancellationToken cancellationToken)
    {
        await foreach (var node in LoadSourcesAsync(cancellationToken)) Nodes.Add(node);
        await base.ViewLoaded(cancellationToken);
    }

    protected internal override Task ViewUnloaded()
    {
        Nodes.Dispose();
        return base.ViewUnloaded();
    }

    internal partial class NodeWrapper(
        string title,
        Metadata metadata,
        NodeMetadata nodeMetadata,
        List<Version> versions) : ObservableObject
    {
        public string Title { get; } = title;

        public Metadata Metadata { get; } = metadata;

        public NodeMetadata NodeMetadata { get; } = nodeMetadata;

        public List<Version> Versions { get; } = versions;

        [ObservableProperty]
        public partial Version SelectedVersion { get; set; } = versions[0];

        public AsyncProperty<string?> ReadmeMarkdown { get; } = new(
            async () =>
            {
                if (!Uri.TryCreate(nodeMetadata.Readme, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not "http" and not "https") return null;

                var response = await App.Resolve<HttpClient>().GetAsync(uri);
                return await response.Content.ReadAsStringAsync();
            });
    }
}