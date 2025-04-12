using Nodis.Core.Networking;

namespace Nodis.Core.Interfaces;

public interface IObjectSynchronizationHub
{
    IObservable<ObjectSynchronizationMessage> MessageReceived { get; }

    Task SendMessageAsync(ObjectSynchronizationMessage message, CancellationToken cancellationToken = default);
}
