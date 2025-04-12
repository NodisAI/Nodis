using CommunityToolkit.Mvvm.ComponentModel;

namespace Nodis.Core.Models;

public sealed partial class AsyncProperty<T>(Func<Task<T>> asyncGetter) : ObservableObject
{
    public T? Value
    {
        get
        {
            if (asyncGetter == null) throw new InvalidOperationException("Get is not supported");

            if (hasValue || IsBusy) return field;
            IsBusy = true;
            asyncGetter().ContinueWith(t =>
            {
                if (t.Exception is { } exception) Exception = exception;
                else field = t.Result;

                hasValue = true;
                IsBusy = false;
                OnPropertyChanged();
            });

            return field;
        }
    }

    [ObservableProperty]
    public partial Exception? Exception { get; private set; }

    public bool IsBusy
    {
        get => Interlocked.Read(ref busyFlag) != 0;
        private set => Interlocked.Exchange(ref busyFlag, value ? 1 : 0);
    }

    private long busyFlag;
    private bool hasValue;
}