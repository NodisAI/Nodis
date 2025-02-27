namespace Nodis.Models;

public class AnonymousDisposable(Action action) : IDisposable
{
    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
        action();
    }
}