using Avalonia.Controls;
using Nodis.Extensions;
using Nodis.ViewModels;
using SukiUI.Controls;

namespace Nodis.Views;

public abstract class ReactiveUserControl<TViewModel> : UserControl where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel => DataContext.NotNull<TViewModel>();

    protected ReactiveUserControl()
    {
        App.Resolve<TViewModel>().Bind(this);
    }
}

public abstract class ReactiveSukiWindow<TViewModel> : SukiWindow where TViewModel : ReactiveViewModelBase
{
    public TViewModel ViewModel => DataContext.NotNull<TViewModel>();

    protected ReactiveSukiWindow()
    {
        App.Resolve<TViewModel>().Bind(this);
    }
}