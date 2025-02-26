using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Extensions;
using Nodis.Interfaces;

namespace Nodis.ViewModels;

public class MainWindowViewModel(IServiceProvider serviceProvider) : ReactiveViewModelBase
{
    public ObservableCollection<IMainWindowPage> Pages { get; } = [];

    protected internal override Task ViewLoaded()
    {
        Pages.AddRange(serviceProvider.GetServices<IMainWindowPage>());
        return base.ViewLoaded();
    }

    protected internal override Task ViewUnloaded()
    {
        Pages.Clear();
        return base.ViewUnloaded();
    }
}