using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowDelayNode : WorkflowBuiltInNode
{
    [YamlIgnore]
    public override string Name => "Delay";

    public WorkflowDelayNode()
    {
        ControlInput = new WorkflowNodeControlInputPin();
        ControlOutputs.Add(new WorkflowNodeControlOutputPin());
        DataInputs.Add(new WorkflowNodeDataInputPin("seconds", new WorkflowNodeFloatData { Value = 1f }));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds((float)DataInputs[0].Value!), cancellationToken);
}