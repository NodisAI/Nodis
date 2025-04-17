using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Reflection;
using Nodis.Core.Interfaces;
using Nodis.Core.Threading;

namespace Nodis.Core.Networking;

/// <summary>
/// Once this struct is created, it will simply set target and do nothing.
/// Only when the <see cref="Id"/> property is accessed (serialize or deserialize), it will register the object.
/// </summary>
/// <param name="target"></param>
internal class NetworkObjectTracker(object target)
{
    private static readonly IObjectSynchronizationHub Hub = ServiceLocator.Resolve<IObjectSynchronizationHub>();
    private static readonly TaskFactory SyncTaskFactory = new(new LimitedConcurrencyLevelTaskScheduler(1));
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> TrackedPropertiesCache = new();
    private static readonly ConcurrentDictionary<Guid, WeakReference<NetworkObjectTracker>> TrackingObjects = new();

    static NetworkObjectTracker()
    {
        Hub.MessageReceived.Subscribe(MessageReceivedHandler);
    }

    private static void MessageReceivedHandler(ObjectSynchronizationMessage message)
    {
        if (!TrackingObjects.TryGetValue(message.ObjectId, out var trackerReference) ||
            !trackerReference.TryGetTarget(out var tracker) ||
            tracker.trackedProperties == null) return;
        switch (message)
        {
            case ObjectSynchronizationPropertyMessage propertyMessage:
            {
                foreach (var (propertyName, value) in propertyMessage.Properties)
                {
                    if (!tracker.trackedProperties.TryGetValue(propertyName, out var propertyInfo)) continue;
                    propertyInfo.SetValue(tracker.target, value);
                }
                break;
            }
        }
    }

    public Guid Id
    {
        get
        {
            if (field != Guid.Empty) return field;
            field = Guid.NewGuid();
            Register();
            return field;
        }
        set
        {
            field = value;
            Register();
        }
    }

    public uint Version { get; private set; }

    private readonly object target = target;

    private IReadOnlyDictionary<string, PropertyInfo>? trackedProperties;

    ~NetworkObjectTracker()
    {
        if (Id == Guid.Empty) return;
        if (!TrackingObjects.TryRemove(Id, out _)) return;
        if (target is INotifyPropertyChanged notifyPropertyChanged) notifyPropertyChanged.PropertyChanged -= HandleTargetPropertyChanged;
    }

    private void Register()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException($"{nameof(Id)} cannot be empty.");
        }

        if (!TrackingObjects.TryAdd(Id, new WeakReference<NetworkObjectTracker>(this)))
        {
            throw new InvalidOperationException($"Object with {nameof(Id)} {Id} is already registered.");
        }

        var type = target.GetType();
        if (!TrackedPropertiesCache.TryGetValue(type, out trackedProperties))
        {
            trackedProperties = type
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p is { CanRead: true, CanWrite: true })
                .ToFrozenDictionary(p => p.Name, p => p);
            TrackedPropertiesCache.TryAdd(type, trackedProperties);
        }

        if (target is INotifyPropertyChanged notifyPropertyChanged) notifyPropertyChanged.PropertyChanged += HandleTargetPropertyChanged;

        // todo: collections
    }

    private void HandleTargetPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == null || trackedProperties?.TryGetValue(args.PropertyName, out var propertyInfo) is not true) return;
        Hub.SendMessageAsync(
            new ObjectSynchronizationPropertyMessage
            {
                ObjectId = Id,
                Version = Version,
                // Properties = new Dictionary<string, object>
                // {
                //     [args.PropertyName] = propertyInfo.GetValue(target)
                // }
            });
    }
}