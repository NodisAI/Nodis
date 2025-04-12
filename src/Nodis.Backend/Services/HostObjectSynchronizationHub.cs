using System.Reactive.Subjects;
using Microsoft.AspNetCore.SignalR;
using Nodis.Core.Interfaces;
using Nodis.Core.Networking;

namespace Nodis.Backend.Services;

public class HostObjectSynchronizationHub : Hub<IObjectSynchronizationHub>, IObjectSynchronizationHub
{
    private readonly Subject<ObjectSynchronizationMessage> messageReceivedSubject = new();

    public IObservable<ObjectSynchronizationMessage> MessageReceived => messageReceivedSubject;

    public Task SendMessageAsync(ObjectSynchronizationMessage message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}