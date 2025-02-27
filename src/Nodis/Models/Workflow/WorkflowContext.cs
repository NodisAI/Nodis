using System.Collections.ObjectModel;

namespace Nodis.Models.Workflow;

public class WorkflowContext
{
    public ObservableCollection<WorkflowNode> Nodes { get; } = [];

    public ObservableCollection<WorkflowNodePortConnection> Connections { get; } = [];
}