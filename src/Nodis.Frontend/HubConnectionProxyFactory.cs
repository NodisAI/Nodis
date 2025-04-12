using System.Reactive.Linq;
using System.Reactive.Subjects;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.SignalR.Client;

namespace Nodis.Frontend;

public class HubConnectionProxyFactory
{
    public static T CreateHubProxy<T>() where T : class
    {
        var connection = new HubConnectionBuilder().WithUrl($"http://localhost:7890/{typeof(T).Name.TrimStart('I')}").Build();
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<T>(new Interceptor<T>(connection));
        // connection.StartAsync();
        return proxy;
    }

    private class Interceptor<T>(HubConnection connection) : IInterceptor where T : class
    {
        private readonly Dictionary<string, object> observables = new();

        public void Intercept(IInvocation invocation)
        {
            if (invocation.Method.IsSpecialName && invocation.Method.Name.StartsWith("get_"))
            {
                HandlePropertyGet(invocation);
            }
            else
            {
                HandleMethodCall(invocation);
            }
        }

        private void HandlePropertyGet(IInvocation invocation)
        {
            var propertyName = invocation.Method.Name[4..]; // remove `get_` prefix
            var returnType = invocation.Method.ReturnType;

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IObservable<>))
            {
                var observable = GetOrCreateObservable(propertyName, returnType.GetGenericArguments()[0]);
                invocation.ReturnValue = observable;
            }
        }

        private object GetOrCreateObservable(string propertyName, Type messageType)
        {
            var key = $"{propertyName}:{messageType.FullName}";
            if (observables.TryGetValue(key, out var observable)) return observable;

            // Get the method info for the generic `On` method
            var onMethod = typeof(HubConnectionExtensions)
                .GetMethods()
                .First(
                    m => m is { Name: nameof(HubConnectionExtensions.On), IsGenericMethod: true } &&
                        m.GetParameters()[2] is { } actionParameter &&
                        actionParameter.ParameterType.GetGenericTypeDefinition() == typeof(Action<>) &&
                        actionParameter.ParameterType.GetGenericArguments().Length == 1)
                .MakeGenericMethod(messageType);

            var subjectType = typeof(Subject<>).MakeGenericType(messageType);
            var subject = Activator.CreateInstance(subjectType)!; // subjectType is not Nullable<T> so return non-nullable
            onMethod.Invoke(null, [connection, propertyName, CreateMessageHandler(messageType, subject)]);

            var asObservableMethod = typeof(Observable).GetMethod(nameof(Observable.AsObservable))!.MakeGenericMethod(messageType);
            return observables[key] = asObservableMethod.Invoke(null, [subject])!;
        }

        private static Delegate CreateMessageHandler(Type messageType, object subject)
        {
            var onNextMethod = subject.GetType().GetMethod(nameof(Subject<T>.OnNext))!;
            return Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(messageType), subject, onNextMethod);
        }

        private void HandleMethodCall(IInvocation invocation)
        {
            if (invocation.Method.ReturnType == typeof(Task))
            {
                var (args, cancellationToken) = GetArgsAndCancellationToken();
                invocation.ReturnValue = connection.InvokeCoreAsync(
                    invocation.Method.Name,
                    args,
                    cancellationToken);
            }
            else if (invocation.Method.ReturnType.IsGenericType &&
                     invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = invocation.Method.ReturnType.GetGenericArguments()[0];
                var invokeMethod = typeof(HubConnectionExtensions)
                    .GetMethods()
                    .First(m => m is { Name: nameof(HubConnectionExtensions.InvokeCoreAsync), IsGenericMethod: true })
                    .MakeGenericMethod(resultType);
                var (args, cancellationToken) = GetArgsAndCancellationToken();
                invocation.ReturnValue = invokeMethod.Invoke(
                    null,
                    [connection, invocation.Method.Name, args, cancellationToken]);
            }

            (object?[] args, CancellationToken cancellationToken) GetArgsAndCancellationToken()
            {
                if (invocation.Arguments.LastOrDefault() is CancellationToken cancellationToken)
                {
                    return (invocation.Arguments[..^1], cancellationToken);
                }

                return (invocation.Arguments, CancellationToken.None);
            }
        }
    }
}