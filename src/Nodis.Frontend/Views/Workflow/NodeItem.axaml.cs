using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPacks.Avalonia.EvaIcons;
using Nodis.Frontend.Extensions;
using ObservableCollections;
using SukiUI.ColorTheme;

namespace Nodis.Frontend.Views;

public enum NodeItemPortEventType
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

public abstract class NodePinWrapper : ObservableObject
{
    public abstract NodePin Pin { get; }

    public static Color GetFillColor(NodePin pin) => pin switch
    {
        { IsConnected: true } => GetStrokeColor(pin),
        _ => Colors.Transparent
    };

    public static Color GetStrokeColor(NodePin pin) => pin switch
    {
        NodeDataPin dataPin => OpenColors.Column6[(int)dataPin.Data.Type % OpenColors.Column6.Length],
        _ => Color.FromRgb(255, 255, 255),
    };

    public static double GetStrokeThickness(NodePin pin) => pin switch
    {
        NodeControlInputPin => 5d,
        NodeControlOutputPin => 5d,
        NodeDataPin => 3d,
        _ => 0d
    };
}

public class NodePinWrapper<T> : NodePinWrapper where T : NodePin
{
    public T TypedPin { get; }
    public override NodePin Pin => TypedPin;
    public Color FillColor => GetFillColor(TypedPin);
    public Color StrokeColor => GetStrokeColor(TypedPin);
    public double StrokeThickness => GetStrokeThickness(TypedPin);

    private NodePinWrapper(T pin)
    {
        TypedPin = pin;
        pin.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(NodePin.IsConnected):
                    OnPropertyChanged(nameof(FillColor));
                    break;
                case nameof(NodeDataPin.Data):
                    OnPropertyChanged(nameof(FillColor));
                    OnPropertyChanged(nameof(StrokeColor));
                    break;
            }
        };
    }

    public static NodePinWrapper<T> Create(T pin) => new(pin);
}

public record WorkflowNodeMenuFlyoutItem(
    string Header,
    PackIconEvaIconsKind Icon,
    ICommand? Command,
    object? CommandParameter = null,
    NotificationType Type = NotificationType.Information)
{
    public static WorkflowNodeMenuFlyoutItem Separator => new("-", 0, null);

    public SolidColorBrush Foreground => Type switch
    {
        NotificationType.Information => new SolidColorBrush(Color.FromRgb(255, 255, 255)),
        NotificationType.Success => new SolidColorBrush(Color.FromRgb(82, 196, 26)),
        NotificationType.Warning => new SolidColorBrush(Color.FromRgb(255, 169, 64)),
        NotificationType.Error => new SolidColorBrush(Color.FromRgb(255, 77, 79)),
        _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
    };

    public ObservableCollection<WorkflowNodeMenuFlyoutItem> Items { get; } = [];
}

public record WorkflowNodeItemPinEventArgs(
    PointerEventArgs PointerEventArgs,
    NodeItemPortEventType Type,
    NodePin FromPin,
    NodePin? ToPin);

public delegate void NodeItemPinEventHandler(NodeItem sender, WorkflowNodeItemPinEventArgs e);

[TemplatePart(Name = ControlInputPinPanelName, Type = typeof(Panel), IsRequired = true)]
[TemplatePart(Name = ControlOutputPinItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
[TemplatePart(Name = DataInputPinItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
[TemplatePart(Name = DataOutputPinItemsControlName, Type = typeof(ItemsControl), IsRequired = true)]
public partial class NodeItem(Node node) : TemplatedControl
{
    private const string ControlInputPinPanelName = "PART_ControlInputPinPanel";
    private const string ControlOutputPinItemsControlName = "PART_ControlOutputPinItemsControl";
    private const string DataInputPinItemsControlName = "PART_DataInputPinItemsControl";
    private const string DataOutputPinItemsControlName = "PART_DataOutputPinItemsControl";

    public Node Node => node;

    public NodePinWrapper<NodeControlInputPin>? ControlInput =>
        node.ControlInput is { } controlInput ? NodePinWrapper<NodeControlInputPin>.Create(controlInput) : null;

    public IList<NodePinWrapper<NodeDataInputPin>> DataInputs => node.DataInputs.CreateView(NodePinWrapper<NodeDataInputPin>.Create)
        .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    public IList<NodePinWrapper<NodeControlOutputPin>> ControlOutputs => node.ControlOutputs.CreateView(NodePinWrapper<NodeControlOutputPin>.Create)
        .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    public IList<NodePinWrapper<NodeDataOutputPin>> DataOutputs => node.DataOutputs.CreateView(NodePinWrapper<NodeDataOutputPin>.Create)
        .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    public IEnumerable<WorkflowNodeMenuFlyoutItem> ContextMenuItems
    {
        get
        {
            if (node is StartNode) yield break;

            yield return new WorkflowNodeMenuFlyoutItem(
                "Remove",
                PackIconEvaIconsKind.Trash2,
                RemoveNodeCommand,
                node,
                NotificationType.Error);

            if (node is VariableNode variableNode)
            {
                yield return WorkflowNodeMenuFlyoutItem.Separator;
                for (var i = 0; i < VariableNode.SupportedDataTypes.Length; i++)
                {
                    var dataType = VariableNode.SupportedDataTypes[i];
                    yield return new WorkflowNodeMenuFlyoutItem(
                        $"Add {dataType.ToFriendlyString()}",
                        dataType switch
                        {
                            NodeDataType.String => PackIconEvaIconsKind.Text,
                            NodeDataType.Int64 => PackIconEvaIconsKind.Hash,
                            NodeDataType.Double => PackIconEvaIconsKind.Percent,
                            NodeDataType.Boolean => PackIconEvaIconsKind.Checkmark,
                            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
                        },
                        variableNode.AddConstantCommand,
                        dataType);
                }

                if (variableNode.Properties.Count == 0) yield break;
                yield return WorkflowNodeMenuFlyoutItem.Separator;
                foreach (var property in variableNode.Properties)
                {
                    yield return new WorkflowNodeMenuFlyoutItem(
                        $"Remove {property.Name}",
                        PackIconEvaIconsKind.Trash2,
                        variableNode.RemoveConstantCommand,
                        property,
                        NotificationType.Error);
                }
            }
        }
    }

    public event NodeItemPinEventHandler? PortEvent;

#if DEBUG
    // For XAML Previewer and unit tests
    public NodeItem() : this(new VariableNode()) { }
#endif

    private Panel? controlInputPinPanel;
    private ItemsControl? controlOutputPinItemsControl, dataInputPinItemsControl, dataOutputPinItemsControl;

    public Point GetPortRelativePoint(NodePin pin)
    {
        switch (pin)
        {
            case NodeControlInputPin:
            {
                var container = controlInputPinPanel;
                if (container == null) return default;
                return container.TranslatePoint(new Point(15, 10), this) ?? default;
            }
            case NodeControlOutputPin when controlOutputPinItemsControl != null:
            {
                var index = ControlOutputs.FindIndexOf(w => w.Pin == pin);
                var container = controlOutputPinItemsControl.ContainerFromIndex(index);
                if (container == null) return default;
                return container.TranslatePoint(new Point(container.Bounds.Width - 15, 10), this) ?? default;
            }
            case NodeDataInputPin when dataInputPinItemsControl != null:
            {
                var index = DataInputs.FindIndexOf(w => w.Pin == pin);
                var container = dataInputPinItemsControl.ContainerFromIndex(index);
                if (container == null) return default;
                return container.TranslatePoint(new Point(15, 10), this) ?? default;
            }
            case NodeDataOutputPin when dataOutputPinItemsControl != null:
            {
                var index = DataOutputs.FindIndexOf(w => w.Pin == pin);
                var container = dataOutputPinItemsControl.ContainerFromIndex(index);
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

    public class NodeRemovedEventArgs(Node node) : RoutedEventArgs
    {
        public Node Node { get; } = node;
    }

    public static readonly RoutedEvent<NodeRemovedEventArgs> NodeRemovedEvent =
        RoutedEvent.Register<NodeItem, NodeRemovedEventArgs>(nameof(NodeRemoved), RoutingStrategies.Bubble);

    public event EventHandler<NodeRemovedEventArgs>? NodeRemoved
    {
        add => AddHandler(NodeRemovedEvent, value);
        remove => RemoveHandler(NodeRemovedEvent, value);
    }

    [RelayCommand]
    private void RemoveNode(Node target) =>
        RaiseEvent(new NodeRemovedEventArgs(target)
        {
            RoutedEvent = NodeRemovedEvent,
            Source = this
        });

    private static NodePin? connectingPin;
    private NodePin? draggingPin;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (Node.Owner?.State == NodeStates.Running) return;

        if (e.Source is Panel { Name: "PART_ControlOutputPin" or "PART_DataOutputPin", DataContext: NodePinWrapper wrapper })
        {
            draggingPin = wrapper.Pin;
            e.Handled = true;
            PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Dragging, wrapper.Pin, null));
        }
        else if (e.Source is not Border { Name: "PART_DraggableRoot" or "PART_StatusIndicator" })
        {
            e.Handled = true;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (draggingPin != null)
        {
            if (Node.Owner?.State == NodeStates.Running)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Drop, draggingPin, null));
                return;
            }

            e.Handled = true;
            connectingPin = null;
            if (Parent is not Canvas parent) return;

            var mouseOverItem = parent.GetVisualsAt(e.GetPosition(parent)).FirstOrDefault().FindParent<NodeItem, Canvas>();
            if (mouseOverItem == null || mouseOverItem == this)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Dragging, draggingPin, null));
                return;
            }

            var nearestDistance = 900d; // 30 pixels
            var relativePoint = e.GetPosition(mouseOverItem);
            switch (draggingPin)
            {
                case NodeControlOutputPin when mouseOverItem is { Node.ControlInput: { } controlInputPin }:
                {
                    var distance = (mouseOverItem.GetPortRelativePoint(controlInputPin) - relativePoint).LengthSquared();
                    if (distance < nearestDistance) connectingPin = controlInputPin;
                    break;
                }
                case NodeDataOutputPin:
                {
                    foreach (var port in mouseOverItem.Node.DataInputs)
                    {
                        var distance = (mouseOverItem.GetPortRelativePoint(port) - relativePoint).LengthSquared();
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            connectingPin = port;
                        }
                    }
                    break;
                }
            }

            if (connectingPin == null)
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Dragging, draggingPin, null));
            }
            else
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Connecting, draggingPin, connectingPin));
            }
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (draggingPin != null)
        {
            if (Node.Owner?.State == NodeStates.Running)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Drop, draggingPin, null));
                return;
            }

            e.Handled = true;
            if (connectingPin == null)
            {
                PortEvent?.Invoke(this, new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Drop, draggingPin, null));
            }
            else
            {
                PortEvent?.Invoke(
                    this,
                    new WorkflowNodeItemPinEventArgs(e, NodeItemPortEventType.Connected, draggingPin, connectingPin));
                connectingPin = null;
            }
            draggingPin = null;
        }

        base.OnPointerReleased(e);
    }

    #endregion

}