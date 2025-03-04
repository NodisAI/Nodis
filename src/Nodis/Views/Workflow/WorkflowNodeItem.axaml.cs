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
    /// Dragging the port.
    /// </summary>
    Dragging,
    /// <summary>
    /// Drop the port but not connected.
    /// </summary>
    Drop,
    /// <summary>
    /// Dragging the port, and it is snapped to another port.
    /// </summary>
    Connecting,
    /// <summary>
    /// Drop the port and connected.
    /// </summary>
    Connected
}

public record WorkflowNodeItemPortEventArgs(
    PointerEventArgs PointerEventArgs,
    WorkflowNodeItemPortEventType Type,
    WorkflowNodePort StartPort,
    WorkflowNodePort? EndPort);

public delegate void WorkflowNodeItemPortEventHandler(WorkflowNodeItem sender, WorkflowNodeItemPortEventArgs e);

[TemplatePart(Name = ControlInputPortPanelName, Type = typeof(Panel), IsRequired = true)]
[TemplatePart(Name = ControlOutputPortItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
[TemplatePart(Name = DataInputPortItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
[TemplatePart(Name = DataOutputPortItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
public class WorkflowNodeItem(WorkflowNode node) : TemplatedControl
{
    private const string ControlInputPortPanelName = "PART_ControlInputPortPanel";
    private const string ControlOutputPortItemsControlName = "PART_ControlOutputPortItemsControl";
    private const string DataInputPortItemsControlName = "PART_DataInputPortItemsControl";
    private const string DataOutputPortItemsControlName = "PART_DataOutputPortItemsControl";

    public WorkflowNode Node => node;

    public event WorkflowNodeItemPortEventHandler? PortEvent;

#if DEBUG
    // For XAML Previewer and unit tests
    public WorkflowNodeItem() : this(new WorkflowConstantNode()) { }
#endif

    private Panel? controlInputPortPanel;
    private ItemsControl? controlOutputPortItemsControl, dataInputPortItemsControl, dataOutputPortItemsControl;

    public Point GetPortRelativePoint(WorkflowNodePort port)
    {
        switch (port)
        {
            case WorkflowNodeControlInputPort:
            {
                var container = controlInputPortPanel;
                if (container == null) return default;
                return container.TranslatePoint(new Point(15, 10), this) ?? default;
            }
            case WorkflowNodeControlOutputPort:
            {
                var container = controlOutputPortItemsControl?.ContainerFromItem(port);
                if (container == null) return default;
                return container.TranslatePoint(new Point(container.Bounds.Width - 15, 10), this) ?? default;
            }
            case WorkflowNodeDataInputPort:
            {
                var container = dataInputPortItemsControl?.ContainerFromItem(port);
                if (container == null) return default;
                return container.TranslatePoint(new Point(15, 10), this) ?? default;
            }
            case WorkflowNodeDataOutputPort:
            {
                var container = dataOutputPortItemsControl?.ContainerFromItem(port);
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

        controlInputPortPanel = e.NameScope.Find<Panel>(ControlInputPortPanelName);
        controlOutputPortItemsControl = e.NameScope.Find<ItemsControl>(ControlOutputPortItemsControlName);
        dataInputPortItemsControl = e.NameScope.Find<ItemsControl>(DataInputPortItemsControlName);
        dataOutputPortItemsControl = e.NameScope.Find<ItemsControl>(DataOutputPortItemsControlName);
    }

    #region Events

    private static WorkflowNodePort? connectingPort;
    private WorkflowNodePort? draggingPort;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (Node.Owner?.State == WorkflowNodeStates.Running) return;

        if (e.Source is Panel { Name: "PART_ControlOutputPort" or "PART_DataOutputPort", DataContext: WorkflowNodePort port })
        {
            draggingPort = port;
            e.Handled = true;
            PortEvent?.Invoke(this, new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Dragging, port, null));
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
                PortEvent?.Invoke(this, new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Drop, draggingPort, null));
                return;
            }

            e.Handled = true;
            connectingPort = null;
            if (Parent is not Canvas parent) return;

            var mouseOverItem = parent.GetVisualsAt(e.GetPosition(parent)).FirstOrDefault().FindParent<WorkflowNodeItem, Canvas>();
            if (mouseOverItem == null || mouseOverItem == this)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Dragging, draggingPort, null));
                return;
            }

            var nearestDistance = 900d; // 30 pixels
            var relativePoint = e.GetPosition(mouseOverItem);
            switch (draggingPort)
            {
                case WorkflowNodeControlOutputPort when mouseOverItem is { Node.ControlInput: { } controlInputPort }:
                {
                    var distance = (mouseOverItem.GetPortRelativePoint(controlInputPort) - relativePoint).LengthSquared();
                    if (distance < nearestDistance) connectingPort = controlInputPort;
                    break;
                }
                case WorkflowNodeDataOutputPort:
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
                    new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Dragging, draggingPort, null));
            }
            else
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Connecting, draggingPort, connectingPort));
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
                PortEvent?.Invoke(this, new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Drop, draggingPort, null));
                return;
            }

            e.Handled = true;
            if (connectingPort == null)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Drop, draggingPort, null));
            }
            else
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPortEventArgs(e, WorkflowNodeItemPortEventType.Connected, draggingPort, connectingPort));
                connectingPort = null;
            }
            draggingPort = null;
        }

        base.OnPointerReleased(e);
    }

    #endregion

}