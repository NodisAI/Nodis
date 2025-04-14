using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using MessagePack;
using Nodis.Core.Networking;
using ObservableCollections;

namespace Nodis.Core.Models.Workflow;

public class NodePropertyList<T> : ObservableList<T> where T : NodeMember
{
    [IgnoreMember]
    internal readonly NetworkObjectTracker tracker;

    [field: AllowNull, MaybeNull]
    [IgnoreMember]
    public NotifyCollectionChangedSynchronizedViewList<T> Bindable =>
        field ??= this.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    [IgnoreMember]
    private readonly Node owner;

    public NodePropertyList(Node owner)
    {
        tracker = new NetworkObjectTracker(this);
        this.owner = owner;
        CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(in NotifyCollectionChangedEventArgs<T> e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.IsSingleItem:
            {
                HandlePropertyAdded(e.NewItem);
                break;
            }
            case NotifyCollectionChangedAction.Add:
            {
                foreach (var property in e.NewItems) HandlePropertyAdded(property);
                break;
            }
            case NotifyCollectionChangedAction.Remove when e.IsSingleItem:
            {
                HandlePropertyRemoved(e.OldItem);
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                foreach (var property in e.OldItems) HandlePropertyRemoved(property);
                break;
            }
            case NotifyCollectionChangedAction.Replace when e.IsSingleItem:
            {
                HandlePropertyRemoved(e.OldItem);
                HandlePropertyAdded(e.NewItem);
                break;
            }
            case NotifyCollectionChangedAction.Replace:
            {
                foreach (var property in e.OldItems) HandlePropertyRemoved(property);
                foreach (var property in e.NewItems) HandlePropertyAdded(property);
                break;
            }
            case NotifyCollectionChangedAction.Reset:
            {
                foreach (var property in this) HandlePropertyRemoved(property);
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandlePropertyAdded(T property)
    {
        if (property.Owner != null) throw new InvalidOperationException("Property already has an owner");
        property.Id = property.Id switch
        {
            0 => owner.GetAvailableMemberId(),
            _ => ContainsId(owner.Id) ? throw new InvalidOperationException($"Property with id '{property.Id}' already exists") : property.Id
        };
        property.Owner = owner;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HandlePropertyRemoved(T property)
    {
        property.Owner = null;
        property.Id = 0;
    }

    public bool ContainsId(ulong id) => this.Any(nodeMember => nodeMember.Id == id);

    public bool ContainsName(string name) => this.Any(nodeMember => nodeMember.Name == name);

    public T this[string name] => this.FirstOrDefault(p => p.Name == name) ?? throw new KeyNotFoundException($"Object of name '{name}' not found");
}