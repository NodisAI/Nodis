using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Nodis.ViewModels;

public abstract partial class ReactiveViewModelBase(string? title = null) : ObservableValidator
{
    [ObservableProperty]
    public partial string? Title { get; set; } = title;
    protected internal ISukiDialogManager DialogManager { get; } = App.Resolve<ISukiDialogManager>();
    protected internal ISukiToastManager ToastManager { get; } = App.Resolve<ISukiToastManager>();

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotifyPropertyNames)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(propertyName);
        foreach (var notify in alsoNotifyPropertyNames) OnPropertyChanged(notify);
        return true;
    }

    protected internal virtual Task ViewLoaded() => Task.CompletedTask;

    protected internal virtual Task ViewUnloaded() => Task.CompletedTask;

    protected virtual IExceptionHandler? LifetimeExceptionHandler => null;

    private void HandleLifetimeException(string stage, Exception e)
    {
        if (LifetimeExceptionHandler is not { } lifetimeExceptionHandler)
        {
            var toast = ToastManager.CreateToast();
            toast.SetType(NotificationType.Error);
            toast.SetTitle("Lifetime Exception");
            toast.SetContent($"[{stage}] {e}");
            toast.Queue();
        }
        else
        {
            lifetimeExceptionHandler.HandleException(e, stage);
        }
    }

    internal void BindUnchecked(StyledElement target)
    {
        target.DataContext = this;
        target.AttachedToLogicalTree += async (_, _) =>
        {
            try
            {
                await ViewLoaded();
            }
            catch (Exception e)
            {
                HandleLifetimeException(nameof(ViewLoaded), e);
            }
        };

        target.DetachedFromLogicalTree += async (_, _) =>
        {
            try
            {
                await ViewUnloaded();
            }
            catch (Exception e)
            {
                HandleLifetimeException(nameof(ViewUnloaded), e);
            }
        };
    }
}