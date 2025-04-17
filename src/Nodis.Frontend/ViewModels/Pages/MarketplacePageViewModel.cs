using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nodis.Frontend.Views;
using ObservableCollections;
using SukiUI.Controls;

namespace Nodis.Frontend.ViewModels;

public partial class MarketplacePageViewModel(IEnvironmentManager environmentManager) : BusyViewModelBase
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<BundleWrapper> Bundles =>
        field ??= bundles.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<BundleWrapper> bundles = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBundleSelected))]
    [NotifyCanExecuteChangedFor(nameof(EditBundleEnvironmentVariablesCommand))]
    [NotifyCanExecuteChangedFor(nameof(InstallBundleCommand))]
    public partial BundleWrapper? SelectedBundle { get; set; }

    public bool IsBundleSelected => SelectedBundle is not null;

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [RelayCommand]
    private Task RefreshSources(CancellationToken cancellationToken) => ExecuteBusyTaskAsync(
        async () =>
        {
            bundles.Clear();
            await foreach (var node in LoadSourcesAsync(cancellationToken)) bundles.Add(node);
        },
        DialogExceptionHandler,
        cancellationToken: cancellationToken);

    [RelayCommand(CanExecute = nameof(IsBundleSelected))]
    private Task EditBundleEnvironmentVariablesAsync()
    {
        if (SelectedBundle is not { } selectedNode) return Task.CompletedTask;

        var valueWithDescriptions = new List<ValueWithDescriptionBase>();
        foreach (var runtime in selectedNode.BundleManifest.Runtimes)
        {
            switch (runtime)
            {
                case McpBundleRuntimeConfiguration { TransportConfiguration: StdioMcpTransportConfiguration { EnvironmentVariables: { } stdioEnvs } }:
                {
                    stdioEnvs.ForEach(p => valueWithDescriptions.Add(p.Value));
                    break;
                }
                case McpBundleRuntimeConfiguration { TransportConfiguration: SseMcpTransportConfiguration { Headers: { } sseHeaders } }:
                {
                    sseHeaders.ForEach(p => valueWithDescriptions.Add(p.Value));
                    break;
                }
            }
        }

        if (valueWithDescriptions.Count == 0) return Task.CompletedTask;

        var dialog = new SukiDialog
        {
            Title = "Edit Environment Variables",
            Content = new GroupBox
            {
                Header = "Follow the instructions to edit the environment variables, or the bundle may not work properly.",
                Content = new ListBox
                {
                    ItemsSource = valueWithDescriptions,
                    ItemTemplate = new FuncDataTemplate<ValueWithDescriptionBase>(
                        (v, _) => new ValueWithDescriptionInput
                        {
                            [!ValueWithDescriptionInput.ValueWithDescriptionProperty] = new Binding { Source = v }
                        })
                }
            }
        };
        if (!DialogManager.TryShowDialog(dialog)) return Task.CompletedTask;

        var taskCompletionSource = new TaskCompletionSource();
        dialog.OnDismissed += _ => taskCompletionSource.TrySetResult();
        return taskCompletionSource.Task;
    }

    [RelayCommand(CanExecute = nameof(IsBundleSelected))]
    private async Task InstallBundleAsync(CancellationToken cancellationToken)
    {
        if (SelectedBundle is not { } selectedNode) return;
        await EditBundleEnvironmentVariablesAsync();
        await environmentManager.InstallBundleAsync(selectedNode.Metadata, selectedNode.BundleManifest, cancellationToken);
    }

    private async IAsyncEnumerable<BundleWrapper> LoadSourcesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await environmentManager.UpdateSourcesAsync(cancellationToken);
        var sources = await environmentManager.EnumerateBundleManifestMetadataAsync();
        foreach (var group in sources.GroupBy(m => $"{m.Namespace}:{m.Name}"))
        {
            var items = group.ToList();
            yield return new BundleWrapper(
                items[0].Name,
                items[0],
                await environmentManager.LoadBundleManifestAsync(items[0], cancellationToken),
                items.Select(p => p.Version).ToList());
        }
    }

    protected internal override async Task ViewLoaded(CancellationToken cancellationToken)
    {
        await RefreshSources(cancellationToken);
        await base.ViewLoaded(cancellationToken);
    }

    protected internal override Task ViewUnloaded()
    {
        Bundles.Dispose();
        return base.ViewUnloaded();
    }

    public partial class BundleWrapper(
        string title,
        Metadata metadata,
        BundleManifest bundleManifest,
        List<SemanticVersion> versions) : ObservableObject
    {
        public string Title { get; } = title;

        public Metadata Metadata { get; } = metadata;

        public BundleManifest BundleManifest { get; } = bundleManifest;

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
                if (!Uri.TryCreate(bundleManifest.Readme, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not "http" and not "https") return null;

                var response = await ServiceLocator.Resolve<HttpClient>().GetAsync(uri);
                return await response.Content.ReadAsStringAsync();
            });
    }
}