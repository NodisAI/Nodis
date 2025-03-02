using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowNodeProperty(string name, WorkflowNodeData data) : WorkflowNodeMember(name)
{
    [YamlMember("data")]
    public WorkflowNodeData Data { get; } = data;
}