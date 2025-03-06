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

public abstract partial class WorkflowNode : ObservableObject
{
    private static int globalId;

    [YamlIgnore]
    public WorkflowContext? Owner { get; internal set; }

    [YamlMember("id")]
    public int Id { get; protected init; }

    public abstract string Name { get; }

    [ObservableProperty]
    [YamlMember("x")]
    public partial double X { get; set; }

    [ObservableProperty]
    [YamlMember("y")]
    public partial double Y { get; set; }

    [YamlIgnore]
    public WorkflowNodeControlInputPin? ControlInput
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
    public WorkflowNodePropertyList<WorkflowNodeControlOutputPin> ControlOutputs { get; }

    [YamlIgnore]
    public WorkflowNodePropertyList<WorkflowNodeDataInputPin> DataInputs { get; }

    [YamlIgnore]
    public WorkflowNodePropertyList<WorkflowNodeDataOutputPin> DataOutputs { get; }

    [YamlIgnore]
    public WorkflowNodePropertyList<WorkflowNodeProperty> Properties { get; }

    [YamlIgnore]
    public virtual object? FooterContent => null;

    [YamlIgnore]
    public virtual IEnumerable<WorkflowNodeMenuFlyoutItem> ContextMenuItems
    {
        get
        {
            yield return new WorkflowNodeMenuFlyoutItem(
                "Remove",
                PackIconEvaIconsKind.Trash2,
                null,  // todo
                this,
                NotificationType.Error);
        }
    }

    [ObservableProperty]
    [YamlIgnore]
    public partial WorkflowNodeStates State { get; protected set; } = WorkflowNodeStates.NotStarted;

    [ObservableProperty]
    [YamlIgnore]
    public partial string? ErrorMessage { get; protected set; }

    protected WorkflowNode(int id)
    {
        Id = id == 0 ? Interlocked.Increment(ref globalId) : id;
        ControlOutputs = new WorkflowNodePropertyList<WorkflowNodeControlOutputPin>(this);
        DataInputs = new WorkflowNodePropertyList<WorkflowNodeDataInputPin>(this);
        DataOutputs = new WorkflowNodePropertyList<WorkflowNodeDataOutputPin>(this);
        Properties = new WorkflowNodePropertyList<WorkflowNodeProperty>(this);
    }

    protected WorkflowNode() : this(Interlocked.Increment(ref globalId)) { }

    public override int GetHashCode() => Id;
    public override bool Equals(object? obj) => obj is WorkflowNode other && Id == other.Id;

    internal int GetAvailableMemberId()
    {
        var id = 0;
        while (true)
        {
            id++;
            if (ControlInput?.Id == id) continue;
            if (ControlOutputs.Any(p => p.Id == id)) continue;
            if (DataInputs.Any(p => p.Id == id)) continue;
            if (DataOutputs.Any(p => p.Id == id)) continue;
            return id;
        }
    }

    public WorkflowNodePin? GetInputPin(int id) =>
        ControlInput?.Id == id ? ControlInput : DataInputs.FirstOrDefault(p => p.Id == id);

    public WorkflowNodePin? GetOutputPin(int id) =>
        ControlOutputs.FirstOrDefault<WorkflowNodePin>(p => p.Id == id) ?? DataOutputs.FirstOrDefault(p => p.Id == id);

    #region Execution

    private CancellationTokenSource? cancellationTokenSource;
    private readonly object executeLock = new();

    internal void Reset()
    {
        lock (executeLock)
        {
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            foreach (var controlOutput in ControlOutputs) controlOutput.CanExecute = null;
            State = WorkflowNodeStates.NotStarted;
        }
    }

    internal void Stop()
    {
        lock (executeLock)
        {
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }
    }

    private async void HandleControlInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkflowNodeControlInputPin.ShouldExecute)) return;
        var shouldExecute = sender.To<WorkflowNodeControlInputPin>()!.ShouldExecute;

        CancellationToken cancellationToken;
        lock (executeLock)
        {
            if (State == WorkflowNodeStates.Running && shouldExecute ||
                State != WorkflowNodeStates.Running && !shouldExecute) return;

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