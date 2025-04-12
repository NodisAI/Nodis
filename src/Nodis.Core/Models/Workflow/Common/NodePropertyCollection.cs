using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using ObservableCollections;

namespace Nodis.Core.Models.Workflow;

public class NodePropertyCollection<T> : ObservableList<T> where T : NodeMember
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<T> Bindable =>
        field ??= this.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly Node owner;

    public NodePropertyCollection(Node owner)
    {
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
            < 0 => throw new InvalidOperationException("Property id must be greater than or equal to 0"),
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

    #region Implementation

    public IEnumerable<string> Keys => this.Select<T, string>(nodeMember => nodeMember.Name);

    public IEnumerable<T> Values => this;

    public bool ContainsId(int id) => this.Any(nodeMember => nodeMember.Id == id);

    public bool ContainsName(string name) => this.Any(nodeMember => nodeMember.Name == name);

    public T this[string key] => this.FirstOrDefault(p => p.Name == key) ?? throw new KeyNotFoundException($"Key '{key}' not found");

    #endregion
}