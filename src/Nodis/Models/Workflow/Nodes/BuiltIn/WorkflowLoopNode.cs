using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowLoopNode : WorkflowBuiltInNode
{
    [YamlIgnore]
    public override string Name => "Loop";

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}