using System.ComponentModel;
using System.Reactive.Linq;

namespace Nodis.Core.Extensions;

public static class ObservableExtension
{
    public static IObservable<T> ObservesProperty<TSource, T>(this TSource source, Func<TSource, T> getter, string propertyName)
        where TSource : INotifyPropertyChanged => Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
            h => source.PropertyChanged += h,
            h => source.PropertyChanged -= h)
        .Where(e => e.EventArgs.PropertyName == propertyName)
        .Select(_ => getter(source));
}