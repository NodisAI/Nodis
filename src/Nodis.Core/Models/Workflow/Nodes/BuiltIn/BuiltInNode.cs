using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
[YamlObjectUnion("!if", typeof(ConditionNode))]
[YamlObjectUnion("!delay", typeof(DelayNode))]
[YamlObjectUnion("!display", typeof(DisplayNode))]
[YamlObjectUnion("!file", typeof(FileNode))]
[YamlObjectUnion("!http_request", typeof(HttpRequestNode))]
[YamlObjectUnion("!loop", typeof(LoopNode))]
[YamlObjectUnion("!serializer", typeof(SerializerNode))]
[YamlObjectUnion("!trigger", typeof(TriggerNode))]
public abstract partial class BuiltInNode : Node;