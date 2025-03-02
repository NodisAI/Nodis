using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowDelayNode : WorkflowNode
{
    [YamlIgnore]
    public override string Name => "Delay";

    public WorkflowDelayNode()
    {
        ControlInput = new WorkflowNodeControlInputPort();
        ControlOutputs.Add(new WorkflowNodeControlOutputPort());
        DataInputs.Add(new WorkflowNodeDataInputPort("seconds", new WorkflowNodeFloatData { Value = 1f }, true));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds((float)DataInputs[0].Value!), cancellationToken);
}