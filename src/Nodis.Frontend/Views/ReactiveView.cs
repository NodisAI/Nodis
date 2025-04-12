using Avalonia.Controls;
using Nodis.Frontend.ViewModels;
using SukiUI.Controls;

namespace Nodis.Frontend.Views;

public abstract class ReactiveUserControl<TViewModel> : UserControl where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel => DataContext.NotNull<TViewModel>();

    protected ReactiveUserControl()
    {
        ServiceLocator.Resolve<TViewModel>().Bind(this);
    }
}

public abstract class ReactiveSukiWindow<TViewModel> : SukiWindow where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel => DataContext.NotNull<TViewModel>();

    protected ReactiveSukiWindow()
    {
        ServiceLocator.Resolve<TViewModel>().Bind(this);
    }
}