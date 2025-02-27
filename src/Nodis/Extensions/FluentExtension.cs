﻿using System.ComponentModel;

namespace Nodis.Extensions;

public static class FluentExtension
{
    public static T With<T>(this T t, Action<T> action)
    {
        action(t);
        return t;
    }

    public static T HandlePropertyChanged<T>(this T source, TypedPropertyChangedEventHandler<T> handler)
        where T : INotifyPropertyChanged
    {
        source.PropertyChanged += (sender, e) => handler(sender.NotNull<T>(), e);
        return source;
    }

    public delegate void TypedPropertyChangedEventHandler<in T>(T sender, PropertyChangedEventArgs e);
}