using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Nodis.Models.Workflow;

namespace Nodis.Views.Workflow;

public class WorkflowNodeItem(WorkflowNode node) : TemplatedControl
{
    public WorkflowNode Node => node;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.Source is Panel { Name: "PART_OutputPort", DataContext: WorkflowNodeOutputPort port })
        {
            Focus();
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (Equals(e.Pointer.Captured, this))
        {
            e.Handled = true;
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (Equals(e.Pointer.Captured, this))
        {
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        base.OnPointerReleased(e);
    }
}