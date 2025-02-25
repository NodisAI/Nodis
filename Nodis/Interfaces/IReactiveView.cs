using Nodis.Extensions;
using Nodis.ViewModels;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Nodis.Interfaces;

public interface IReactiveView
{
    object? DataContext { get; set; }
    ISukiDialogManager DialogManager => DataContext.NotNull<ReactiveViewModelBase>().DialogManager;
    ISukiToastManager ToastManager => DataContext.NotNull<ReactiveViewModelBase>().ToastManager;
}

// ReSharper disable once UnusedTypeParameter
public interface IReactiveView<out TViewModel> : IReactiveView where TViewModel : ReactiveViewModelBase
{
    TViewModel ViewModel => DataContext.NotNull<TViewModel>();
}

public interface IReactiveViewWithServiceFactory<out TViewModel> : IReactiveView<TViewModel> where TViewModel : ReactiveViewModelBase
{
    Func<IServiceProvider, TViewModel> ServiceFactory { get; }
}

public static class ReactiveViewExtension
{
    public static TViewModel GetViewModel<TViewModel>(this IReactiveView<TViewModel> reactiveView) where TViewModel : ReactiveViewModelBase
    {
        return reactiveView.ViewModel;
    }
}