using System.Diagnostics.CodeAnalysis;
using IconPacks.Avalonia.EvaIcons;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Frontend.Interfaces;
using Nodis.Frontend.Views;
using ObservableCollections;
using SukiUI.Controls;

namespace Nodis.Frontend.ViewModels;

public class MainWindowViewModel(IServiceProvider serviceProvider) : ReactiveViewModelBase
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<SukiSideMenuItem> Pages =>
        field ??= pages.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<SukiSideMenuItem> pages = [];

    public DownloadTasksPage DownloadTasksPage { get; } = serviceProvider.GetRequiredService<DownloadTasksPage>();

    protected internal override Task ViewLoaded(CancellationToken cancellationToken)
    {
        pages.Reset(
            serviceProvider.GetServices<IMainWindowPage>().Select(
                p => new SukiSideMenuItem
                {
                    Header = p.Title,
                    PageContent = p,
                    Icon = new PackIconEvaIcons { Width = 24, Height = 24, Kind = p.Icon },
                    IsContentMovable = false
                }));
        return base.ViewLoaded(cancellationToken);
    }

    protected internal override Task ViewUnloaded()
    {
        Pages.Dispose();
        return base.ViewUnloaded();
    }
}