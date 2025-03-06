using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
[YamlObjectUnion("!condition", typeof(WorkflowConditionNode))]
[YamlObjectUnion("!constant", typeof(WorkflowConstantNode))]
[YamlObjectUnion("!delay", typeof(WorkflowDelayNode))]
[YamlObjectUnion("!display", typeof(WorkflowDisplayNode))]
[YamlObjectUnion("!loop", typeof(WorkflowLoopNode))]
[YamlObjectUnion("!start", typeof(WorkflowStartNode))]
public abstract partial class WorkflowBuiltInNode : WorkflowNode;