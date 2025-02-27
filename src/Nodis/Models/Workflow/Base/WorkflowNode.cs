using System.Windows.Input;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using IconPacks.Avalonia.EvaIcons;
using Nodis.Extensions;

namespace Nodis.Models.Workflow;

public enum WorkflowNodeStatus
{
    NotStarted,
    Running,
    Completed,
    Failed
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
    public long Id { get; }

    private static long globalId;

    public abstract string Name { get; }

    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    public WorkflowNodePropertyList<WorkflowNodeProperty> Properties { get; }

    public WorkflowNodePropertyList<WorkflowNodeInputPort> Inputs { get; }

    public WorkflowNodePropertyList<WorkflowNodeOutputPort> Outputs { get; }

    public virtual IEnumerable<WorkflowNodeMenuFlyoutItem> ContextMenuItems
    {
        get
        {
            yield return new WorkflowNodeMenuFlyoutItem("Delete", PackIconEvaIconsKind.Trash2, null, Type: NotificationType.Error);
        }
    }

    [ObservableProperty]
    public partial WorkflowNodeStatus Status { get; protected set; } = WorkflowNodeStatus.NotStarted;

    [ObservableProperty]
    public partial string? ErrorMessage { get; protected set; }

    protected WorkflowNode()
    {
        Id = Interlocked.Increment(ref globalId);
        Properties = new WorkflowNodePropertyList<WorkflowNodeProperty>(this);
        Inputs = new WorkflowNodePropertyList<WorkflowNodeInputPort>(this);
        Outputs = new WorkflowNodePropertyList<WorkflowNodeOutputPort>(this);
    }

    private CancellationTokenSource? cancellationTokenSource;
    private readonly object executeLock = new();

    public async Task ExecuteAsync()
    {
        CancellationToken cancellationToken;
        lock (executeLock)
        {
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }

            Status = WorkflowNodeStatus.Running;
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        try
        {
            await ExecuteImplAsync(cancellationToken);
            Status = WorkflowNodeStatus.Completed;
        }
        catch (Exception e)
        {
            ErrorMessage = e.GetFriendlyMessage();
            Status = WorkflowNodeStatus.Failed;
        }
    }

    protected abstract Task ExecuteImplAsync(CancellationToken cancellationToken);
}