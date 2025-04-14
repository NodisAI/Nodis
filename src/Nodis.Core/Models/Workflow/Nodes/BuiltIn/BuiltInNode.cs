using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
[YamlObjectUnion("!condition", typeof(ConditionNode))]
[YamlObjectUnion("!delay", typeof(DelayNode))]
[YamlObjectUnion("!file", typeof(FileNode))]
[YamlObjectUnion("!http_request", typeof(HttpRequestNode))]
[YamlObjectUnion("!loop", typeof(LoopNode))]
[YamlObjectUnion("!preview", typeof(PreviewNode))]
[YamlObjectUnion("!serializer", typeof(SerializerNode))]
[YamlObjectUnion("!trigger", typeof(TriggerNode))]
[YamlObjectUnion("!variable", typeof(VariableNode))]
public abstract partial class BuiltInNode : Node;