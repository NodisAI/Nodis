using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Nodis.Frontend.ViewModels;

public abstract class ReactiveViewModelBase : ObservableValidator
{
    protected internal ISukiDialogManager DialogManager { get; } = ServiceLocator.Resolve<ISukiDialogManager>();
    protected internal ISukiToastManager ToastManager { get; } = ServiceLocator.Resolve<ISukiToastManager>();

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotifyPropertyNames)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        OnPropertyChanged(propertyName);
        foreach (var notify in alsoNotifyPropertyNames) OnPropertyChanged(notify);
        return true;
    }

    protected internal virtual Task ViewLoaded(CancellationToken cancellationToken) => Task.CompletedTask;

    protected internal virtual Task ViewUnloaded() => Task.CompletedTask;

    protected virtual IExceptionHandler? LifetimeExceptionHandler => null;

    private void HandleLifetimeException(string stage, Exception e)
    {
        if (LifetimeExceptionHandler is not { } lifetimeExceptionHandler)
        {
            ToastManager.CreateToast()
                .SetType(NotificationType.Error)
                .SetTitle($"Lifetime Exception: [{stage}]")
                .SetContent(e.GetFriendlyMessage())
                .SetCanDismissByClicking()
                .Queue();
        }
        else
        {
            lifetimeExceptionHandler.HandleException(e, stage);
        }
    }

    public void Bind(Control target)
    {
        CancellationTokenSource? cancellationTokenSource = null;

        target.DataContext = this;
        target.Loaded += async (_, _) =>
        {
            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                await ViewLoaded(cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                HandleLifetimeException(nameof(ViewLoaded), e);
            }
        };

        target.Unloaded += async (_, _) =>
        {
            try
            {
                await ViewUnloaded();

                if (cancellationTokenSource != null)
                {
                    await cancellationTokenSource.CancelAsync();
                    cancellationTokenSource.Dispose();
                }
            }
            catch (Exception e)
            {
                HandleLifetimeException(nameof(ViewUnloaded), e);
            }
        };
    }
}