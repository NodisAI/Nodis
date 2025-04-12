using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nodis.Core;
using ObservableCollections;

namespace Nodis.Frontend.ViewModels;

public partial class MarketplacePageViewModel(IEnvironmentManager environmentManager) : ReactiveViewModelBase
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
        return environmentManager.InstallPackageAsync(selectedNode.Metadata, CancellationToken.None);
    }

    private async IAsyncEnumerable<NodeWrapper> LoadSourcesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await environmentManager.UpdateSourcesAsync(cancellationToken);
        var sources = await environmentManager.EnumerateSourcesAsync();
        foreach (var group in sources.GroupBy(m => $"{m.Namespace}:{m.Name}"))
        {
            var items = group.ToList();
            yield return new NodeWrapper(
                items[0].Name,
                items[0],
                await environmentManager.LoadSourcePackageAsync(items[0], cancellationToken),
                items.Select(p => p.Version).ToList());
        }
    }

    protected internal override async Task ViewLoaded(CancellationToken cancellationToken)
    {
        await foreach (var node in LoadSourcesAsync(cancellationToken)) nodes.Add(node);
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
        PackageMetadata packageMetadata,
        List<SemanticVersion> versions) : ObservableObject
    {
        public string Title { get; } = title;

        public Metadata Metadata { get; } = metadata;

        public PackageMetadata PackageMetadata { get; } = packageMetadata;

        public List<SemanticVersion> Versions { get; } = versions;

        public SemanticVersion? SelectedVersion
        {
            get;
            set
            {
                if (field == value) return;
                if (value.HasValue && !Versions.Contains(value.Value)) value = Versions.FirstOrDefault();
                field = value;
                OnPropertyChanged();
            }
        } = versions.FirstOrDefault();

        public AsyncProperty<string?> ReadmeMarkdown { get; } = new(
            async () =>
            {
                if (!Uri.TryCreate(packageMetadata.Readme, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not "http" and not "https") return null;

                var response = await ServiceLocator.Resolve<HttpClient>().GetAsync(uri);
                return await response.Content.ReadAsStringAsync();
            });
    }
}