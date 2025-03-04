using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Nodis.Extensions;
using Nodis.Models.Workflow;

namespace Nodis.Views.Workflow;

public enum WorkflowNodeItemPortEventType
{
    /// <summary>
    /// Dragging the pin.
    /// </summary>
    Dragging,
    /// <summary>
    /// Drop the pin but not connected.
    /// </summary>
    Drop,
    /// <summary>
    /// Dragging the pin, and it is snapped to another pin.
    /// </summary>
    Connecting,
    /// <summary>
    /// Drop the pin and connected.
    /// </summary>
    Connected
}

public record WorkflowNodeItemPinEventArgs(
    PointerEventArgs PointerEventArgs,
    WorkflowNodeItemPortEventType Type,
    WorkflowNodePin StartPin,
    WorkflowNodePin? EndPin);

public delegate void WorkflowNodeItemPinEventHandler(WorkflowNodeItem sender, WorkflowNodeItemPinEventArgs e);

[TemplatePart(Name = ControlInputPinPanelName, Type = typeof(Panel), IsRequired = true)]
[TemplatePart(Name = ControlOutputPinItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
[TemplatePart(Name = DataInputPinItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
[TemplatePart(Name = DataOutputPinItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
public class WorkflowNodeItem(WorkflowNode node) : TemplatedControl
{
    private const string ControlInputPinPanelName = "PART_ControlInputPinPanel";
    private const string ControlOutputPinItemsControlName = "PART_ControlOutputPinItemsControl";
    private const string DataInputPinItemsControlName = "PART_DataInputPinItemsControl";
    private const string DataOutputPinItemsControlName = "PART_DataOutputPinItemsControl";

    public WorkflowNode Node => node;

    public event WorkflowNodeItemPinEventHandler? PortEvent;

#if DEBUG
    // For XAML Previewer and unit tests
    public WorkflowNodeItem() : this(new WorkflowConstantNode()) { }
#endif

    private Panel? controlInputPinPanel;
    private ItemsControl? controlOutputPinItemsControl, dataInputPinItemsControl, dataOutputPinItemsControl;

    public Point GetPortRelativePoint(WorkflowNodePin pin)
    {
        switch (pin)
        {
            case WorkflowNodeControlInputPin:
            {
                var container = controlInputPinPanel;
                if (container == null) return default;
                return container.TranslatePoint(new Point(15, 10), this) ?? default;
            }
            case WorkflowNodeControlOutputPin:
            {
                var container = controlOutputPinItemsControl?.ContainerFromItem(pin);
                if (container == null) return default;
                return container.TranslatePoint(new Point(container.Bounds.Width - 15, 10), this) ?? default;
            }
            case WorkflowNodeDataInputPin:
            {
                var container = dataInputPinItemsControl?.ContainerFromItem(pin);
                if (container == null) return default;
                return container.TranslatePoint(new Point(15, 10), this) ?? default;
            }
            case WorkflowNodeDataOutputPin:
            {
                var container = dataOutputPinItemsControl?.ContainerFromItem(pin);
                if (container == null) return default;
                return container.TranslatePoint(new Point(container.Bounds.Width - 15, 10), this) ?? default;
            }
            default:
            {
                return default;
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        controlInputPinPanel = e.NameScope.Find<Panel>(ControlInputPinPanelName);
        controlOutputPinItemsControl = e.NameScope.Find<ItemsControl>(ControlOutputPinItemsControlName);
        dataInputPinItemsControl = e.NameScope.Find<ItemsControl>(DataInputPinItemsControlName);
        dataOutputPinItemsControl = e.NameScope.Find<ItemsControl>(DataOutputPinItemsControlName);
    }

    #region Events

    private static WorkflowNodePin? connectingPort;
    private WorkflowNodePin? draggingPort;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (Node.Owner?.State == WorkflowNodeStates.Running) return;

        if (e.Source is Panel { Name: "PART_ControlOutputPin" or "PART_DataOutputPin", DataContext: WorkflowNodePin port })
        {
            draggingPort = port;
            e.Handled = true;
            PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Dragging, port, null));
        }
        else if (e.Source is not Border { Name: "PART_DraggableRoot" })
        {
            e.Handled = true;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (draggingPort != null)
        {
            if (Node.Owner?.State == WorkflowNodeStates.Running)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Drop, draggingPort, null));
                return;
            }

            e.Handled = true;
            connectingPort = null;
            if (Parent is not Canvas parent) return;

            var mouseOverItem = parent.GetVisualsAt(e.GetPosition(parent)).FirstOrDefault().FindParent<WorkflowNodeItem, Canvas>();
            if (mouseOverItem == null || mouseOverItem == this)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Dragging, draggingPort, null));
                return;
            }

            var nearestDistance = 900d; // 30 pixels
            var relativePoint = e.GetPosition(mouseOverItem);
            switch (draggingPort)
            {
                case WorkflowNodeControlOutputPin when mouseOverItem is { Node.ControlInput: { } controlInputPin }:
                {
                    var distance = (mouseOverItem.GetPortRelativePoint(controlInputPin) - relativePoint).LengthSquared();
                    if (distance < nearestDistance) connectingPort = controlInputPin;
                    break;
                }
                case WorkflowNodeDataOutputPin:
                {
                    foreach (var port in mouseOverItem.Node.DataInputs)
                    {
                        var distance = (mouseOverItem.GetPortRelativePoint(port) - relativePoint).LengthSquared();
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            connectingPort = port;
                        }
                    }
                    break;
                }
            }

            if (connectingPort == null)
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Dragging, draggingPort, null));
            }
            else
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Connecting, draggingPort, connectingPort));
            }
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (draggingPort != null)
        {
            if (Node.Owner?.State == WorkflowNodeStates.Running)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Drop, draggingPort, null));
                return;
            }

            e.Handled = true;
            if (connectingPort == null)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Drop, draggingPort, null));
            }
            else
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPinEventArgs(e, WorkflowNodeItemPortEventType.Connected, draggingPort, connectingPort));
                connectingPort = null;
            }
            draggingPort = null;
        }

        base.OnPointerReleased(e);
    }

    #endregion

}