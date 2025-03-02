using Nodis.Extensions;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public sealed partial class WorkflowStartNode : WorkflowNode
{
    [YamlIgnore]
    public override string Name => "Start";

    [YamlIgnore]
    public override IEnumerable<WorkflowNodeMenuFlyoutItem> ContextMenuItems => [];

    public WorkflowStartNode()
    {
        ControlOutputs.Add(new ControlOutputPort());
    }

    public void Start()
    {
        ControlOutputs[0].To<ControlOutputPort>().Start();
        State = WorkflowNodeStates.Completed;
    }

    public void Stop()
    {
        ControlOutputs[0].To<ControlOutputPort>().Stop();
        State = WorkflowNodeStates.NotStarted;
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        // This node only notifies the next node to start, so it doesn't need to do anything
        throw new NotSupportedException();
    }

    private class ControlOutputPort : WorkflowNodeControlOutputPort
    {
        public void Start() => CanExecute = true;
        public void Stop() => CanExecute = false;
    }
}