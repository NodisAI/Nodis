using CommunityToolkit.Mvvm.ComponentModel;

namespace Nodis.Models;

public sealed partial class AsyncProperty<T> : ObservableObject
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

    public bool IsBusy
    {
        get => Interlocked.Read(ref busyFlag) != 0;
        private set => Interlocked.Exchange(ref busyFlag, value ? 1 : 0);
    }

    private bool hasValue;

    [ObservableProperty]
    public partial Exception? Exception { get; private set; }

    private readonly Func<Task<T>>? asyncGetter;
    private long busyFlag;

    public AsyncProperty(Func<Task<T>> asyncGetter)
    {
        this.asyncGetter = asyncGetter;
    }
}