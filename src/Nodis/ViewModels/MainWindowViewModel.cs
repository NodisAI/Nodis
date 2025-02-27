using System.Collections.ObjectModel;
using IconPacks.Avalonia.EvaIcons;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Extensions;
using Nodis.Interfaces;
using SukiUI.Controls;

namespace Nodis.ViewModels;

public class MainWindowViewModel(IServiceProvider serviceProvider) : ReactiveViewModelBase
{
    public ObservableCollection<SukiSideMenuItem> Pages { get; } = [];

    protected internal override Task ViewLoaded()
    {
        Pages.AddRange(serviceProvider.GetServices<IMainWindowPage>().Select(
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
        Pages.Clear();
        return base.ViewUnloaded();
    }
}