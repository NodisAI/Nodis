using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using ObservableCollections;

namespace Nodis.Models.Workflow;

public class WorkflowNodePropertyList<T> : ObservableList<T> where T : WorkflowNodeMember
{
    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<T> Bindable =>
        field ??= this.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly WorkflowNode owner;

    public WorkflowNodePropertyList(WorkflowNode owner)
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
        property.Owner = owner;
        property.Id = Interlocked.Increment(ref owner.propertyId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HandlePropertyRemoved(T property)
    {
        property.Owner = null;
    }
}