using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using IconPacks.Avalonia.EvaIcons;
using Nodis.Extensions;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[Flags]
public enum WorkflowNodeStates
{
    NotStarted = 0x0,
    Running = 0x1,
    Completed = 0x2,
    Failed = 0x4
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
}

[YamlObject]
[YamlObjectUnion("!condition", typeof(WorkflowConditionNode))]
[YamlObjectUnion("!constant", typeof(WorkflowConstantNode))]
[YamlObjectUnion("!delay", typeof(WorkflowDelayNode))]
[YamlObjectUnion("!loop", typeof(WorkflowLoopNode))]
[YamlObjectUnion("!start", typeof(WorkflowStartNode))]
public abstract partial class WorkflowNode : ObservableObject
{
    private static int globalId;
    internal int propertyId;

    [YamlMember("id")]
    public int Id { get; protected init; }

    [YamlIgnore]
    public abstract string Name { get; }

    [ObservableProperty]
    [YamlMember("x")]
    public partial double X { get; set; }

    [ObservableProperty]
    [YamlMember("y")]
    public partial double Y { get; set; }

    [YamlIgnore]
    public WorkflowNodeControlInputPort? ControlInput
    {
        get;
        protected set
        {
            if (Equals(value, field)) return;
            if (field != null)
            {
                field.Owner = null;
                field.PropertyChanged -= HandleControlInputPropertyChanged;
            }
            field = value;
            if (field != null)
            {
                field.Owner = this;
                field.PropertyChanged += HandleControlInputPropertyChanged;
            }
        }
    }

    [YamlIgnore]
    public WorkflowNodePropertyList<WorkflowNodeControlOutputPort> ControlOutputs { get; }

    [YamlIgnore]
    public WorkflowNodePropertyList<WorkflowNodeDataInputPort> DataInputs { get; }

    [YamlIgnore]
    public WorkflowNodePropertyList<WorkflowNodeDataOutputPort> DataOutputs { get; }

    [YamlIgnore]
    public WorkflowNodePropertyList<WorkflowNodeProperty> Properties { get; }

    [YamlIgnore]
    public virtual IEnumerable<WorkflowNodeMenuFlyoutItem> ContextMenuItems
    {
        get
        {
            yield return new WorkflowNodeMenuFlyoutItem("Delete", PackIconEvaIconsKind.Trash2, null, Type: NotificationType.Error);
        }
    }

    [ObservableProperty]
    [YamlIgnore]
    public partial WorkflowNodeStates State { get; protected set; } = WorkflowNodeStates.NotStarted;

    [ObservableProperty]
    [YamlIgnore]
    public partial string? ErrorMessage { get; protected set; }

    [YamlConstructor]
    private WorkflowNode(int id)
    {
        Id = id;
        ControlOutputs = new WorkflowNodePropertyList<WorkflowNodeControlOutputPort>(this);
        DataInputs = new WorkflowNodePropertyList<WorkflowNodeDataInputPort>(this);
        DataOutputs = new WorkflowNodePropertyList<WorkflowNodeDataOutputPort>(this);
        Properties = new WorkflowNodePropertyList<WorkflowNodeProperty>(this);
    }

    protected WorkflowNode() : this(Interlocked.Increment(ref globalId)) { }

    public override int GetHashCode() => Id;
    public override bool Equals(object? obj) => obj is WorkflowNode other && Id == other.Id;

    public WorkflowNodePort? GetInputPort(int id) =>
        ControlInput?.Id == id ? ControlInput : DataInputs.FirstOrDefault(p => p.Id == id);

    public WorkflowNodePort? GetOutputPort(int id) =>
        ControlOutputs.FirstOrDefault<WorkflowNodePort>(p => p.Id == id) ?? DataOutputs.FirstOrDefault(p => p.Id == id);

    #region Execution

    private CancellationTokenSource? cancellationTokenSource;
    private readonly object executeLock = new();

    public void CancelExecution()
    {
        lock (executeLock)
        {
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            State = WorkflowNodeStates.NotStarted;
        }
    }

    private async void HandleControlInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var shouldExecute = sender.To<WorkflowNodeControlInputPort>()!.ShouldExecute;

        CancellationToken cancellationToken;
        lock (executeLock)
        {
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            if (!shouldExecute) return;

            State = WorkflowNodeStates.Running;
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        try
        {
            await ExecuteImplAsync(cancellationToken);
            State = WorkflowNodeStates.Completed;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.GetFriendlyMessage();
            State = WorkflowNodeStates.Failed;
        }
    }

    protected abstract Task ExecuteImplAsync(CancellationToken cancellationToken);

    #endregion
}