namespace Nodis.Extensions;

public static class FluentExtension
{
    public static T With<T>(this T t, Action<T> action)
    {
        action(t);
        return t;
    }
}