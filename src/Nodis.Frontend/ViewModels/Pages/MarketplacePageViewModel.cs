using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using AsyncImageLoader;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObservableCollections;

namespace Nodis.Frontend.ViewModels;

public partial class MarketplacePageViewModel(
    IEnvironmentManager environmentManager,
    IDownloadTasksManager downloadTasksManager
) : BusyViewModelBase
{
    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<BundleWrapper> Bundles =>
        field ??= bundles.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<BundleWrapper> bundles = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedVersion))]
    [NotifyPropertyChangedFor(nameof(CanInstallBundle))]
    [NotifyCanExecuteChangedFor(nameof(InstallBundleCommand))]
    public partial BundleWrapper? SelectedBundle { get; set; }

    public VersionWrapper? SelectedVersion
    {
        get => SelectedBundle?.SelectedVersion;
        set
        {
            if (SelectedBundle == null) return;
            if (value != null) SelectedBundle.SelectedVersion = value;
            else OnPropertyChanged();
            OnPropertyChanged(nameof(CanInstallBundle));
            InstallBundleCommand.NotifyCanExecuteChanged();
        }
    }

    private readonly Dictionary<Metadata, DownloadTask> downloadTasks = new();

    public bool IsSelectedBundleDownloading => SelectedBundle is not null && downloadTasks.ContainsKey(SelectedBundle.Metadata);

    [RelayCommand]
    private Task RefreshSources(CancellationToken cancellationToken) => ExecuteBusyTaskAsync(
        async () =>
        {
            bundles.Clear();
            await foreach (var node in LoadSourcesAsync(cancellationToken)) bundles.Add(node);
        },
        DialogExceptionHandler,
        cancellationToken: cancellationToken);

    public bool CanInstallBundle => SelectedVersion is { IsInstalled: false };

    [RelayCommand(CanExecute = nameof(CanInstallBundle))]
    private async Task InstallBundleAsync()
    {
        if (SelectedBundle is not { } selectedBundle ||
            SelectedVersion is not { } selectedVersion) return;
        if (IsSelectedBundleDownloading) return;

        var metadata = selectedBundle.Metadata with { Version = selectedVersion.Version };
        var cancellationTokenSource = new CancellationTokenSource();
        var downloadTask = new DownloadTask($"{selectedBundle.Title} ({metadata.Version})");
        downloadTask.DeleteCommand = new RelayCommand(
            () =>
            {
                cancellationTokenSource.Cancel();
                downloadTask.Status = DownloadTaskStatus.Canceled;
                downloadTasksManager.Remove(downloadTask);
            });
        if (Uri.TryCreate(selectedBundle.BundleManifest.Icon, UriKind.Absolute, out var iconUri))
        {
            downloadTask.Icon = new Image
            {
                Source = await ImageLoader.AsyncImageLoader.ProvideImageAsync(iconUri.ToString())
            };
        }
        downloadTasks.Add(metadata, downloadTask);
        downloadTasksManager.Add(downloadTask);
        InstallBundleInternalAsync();

        async void InstallBundleInternalAsync()
        {
            try
            {
                downloadTask.Status = DownloadTaskStatus.InProgress;
                await environmentManager.InstallBundleAsync(
                    metadata,
                    selectedBundle.BundleManifest,
                    downloadTask,
                    cancellationTokenSource.Token);

                // selectedBundle.IsSelectedVersionInstalled = true;
                downloadTask.Progress = 100d;
                downloadTask.Status = DownloadTaskStatus.Completed;
            }
            catch (Exception ex)
            {
                downloadTask.Status = DownloadTaskStatus.Failed;
                downloadTask.ProgressText = ex.GetFriendlyMessage();
            }
            finally
            {
                downloadTasks.Remove(selectedBundle.Metadata);
                // await RefreshSources(cancellationToken);
            }
        }
    }

    private async IAsyncEnumerable<BundleWrapper> LoadSourcesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await environmentManager.UpdateSourcesAsync(cancellationToken);
        var bundleManifests = await environmentManager.EnumerateBundleManifestMetadataAsync();
        var installedBundles = (await environmentManager.EnumerateInstalledBundleMetadataAsync()).ToImmutableHashSet();
        foreach (var group in bundleManifests.GroupBy(m => $"{m.Namespace}:{m.Name}"))
        {
            var items = group.ToList();
            var latest = items.OrderDescending().First();
            yield return new BundleWrapper(
                latest.Name,
                latest,
                await environmentManager.LoadBundleManifestAsync(latest, cancellationToken),
                items.Select(p => new VersionWrapper(p.Version, installedBundles.Contains(p))).ToList());
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

    public class VersionWrapper(SemanticVersion version, bool isInstalled)
    {
        public SemanticVersion Version { get; set; } = version;

        public bool IsInstalled { get; set; } = isInstalled;

        public override string ToString() => Version.ToString();
    }

    public class BundleWrapper(
        string title,
        Metadata metadata,
        BundleManifest bundleManifest,
        List<VersionWrapper> versions) : ObservableObject
    {
        public string Title { get; } = title;

        public Metadata Metadata { get; } = metadata;

        public BundleManifest BundleManifest { get; } = bundleManifest;

        public List<VersionWrapper> Versions { get; } = versions;

        public VersionWrapper? SelectedVersion
        {
            get;
            set
            {
                if (field == value) return;
                if (value != null && !Versions.Contains(value)) value = Versions.FirstOrDefault();
                field = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectedVersionInstalled));
            }
        } = versions.FirstOrDefault();

        public bool IsSelectedVersionInstalled => SelectedVersion?.IsInstalled is true;

        public string? ReadmeMarkdownUrlRoot
        {
            get
            {
                if (BundleManifest.Readme == null) return null;
                var lastSlashIndex = BundleManifest.Readme.LastIndexOf('/');
                if (lastSlashIndex == -1) return null;
                return BundleManifest.Readme[..lastSlashIndex];
            }
        }

        public AsyncProperty<string?> ReadmeMarkdown { get; } = new(
            async () =>
            {
                if (!Uri.TryCreate(bundleManifest.Readme, UriKind.Absolute, out var uri) ||
                    uri.Scheme is not "http" and not "https") return null;

                var response = await ServiceLocator.Resolve<HttpClient>().GetAsync(uri);
                return await response.Content.ReadAsStringAsync();
            });

        public IEnumerable<ValueWithDescriptionBase> EnvironmentVariables
        {
            get
            {
                foreach (var runtime in BundleManifest.Runtimes)
                {
                    switch (runtime)
                    {
                        case McpBundleRuntimeConfiguration
                        {
                            TransportConfiguration: StdioMcpTransportConfiguration { EnvironmentVariables: { } stdioEnvs }
                        }:
                        {
                            foreach (var env in stdioEnvs) yield return env.Value;
                            break;
                        }
                        case McpBundleRuntimeConfiguration { TransportConfiguration: SseMcpTransportConfiguration { Headers: { } sseHeaders } }:
                        {
                            foreach (var env in sseHeaders) yield return env.Value;
                            break;
                        }
                    }
                }
            }
        }
    }
}