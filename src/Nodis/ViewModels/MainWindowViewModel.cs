using System.Diagnostics.CodeAnalysis;
using IconPacks.Avalonia.EvaIcons;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Extensions;
using Nodis.Interfaces;
using ObservableCollections;
using SukiUI.Controls;

namespace Nodis.ViewModels;

public class MainWindowViewModel(IServiceProvider serviceProvider) : ReactiveViewModelBase
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<SukiSideMenuItem> Pages =>
        field ??= pages.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<SukiSideMenuItem> pages = [];

    protected internal override Task ViewLoaded()
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
        return base.ViewLoaded();
    }

    protected internal override Task ViewUnloaded()
    {
        Pages.Dispose();
        return base.ViewUnloaded();
    }
}