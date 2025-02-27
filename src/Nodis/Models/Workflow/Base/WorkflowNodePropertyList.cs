using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Nodis.Models.Workflow;

public class WorkflowNodePropertyList<T>(WorkflowNode owner) : ObservableCollection<T> where T : WorkflowNodeProperty
{
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                HandlePropertyAdded((T)e.NewItems![0]!);
                break;
            }
            case NotifyCollectionChangedAction.Remove:
            {
                HandlePropertyRemoved((T)e.OldItems![0]!);
                break;
            }
            case NotifyCollectionChangedAction.Replace:
            {
                HandlePropertyRemoved((T)e.OldItems![0]!);
                HandlePropertyAdded((T)e.NewItems![0]!);
                break;
            }
        }
        base.OnCollectionChanged(e);
    }

    protected override void ClearItems()
    {
        foreach (var property in this) HandlePropertyRemoved(property);
        base.ClearItems();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandlePropertyAdded(T property)
    {
        property.Owner = owner;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HandlePropertyRemoved(T property)
    {
        property.Owner = null;
    }
}