using MessagePack;
using Nodis.Core.Interfaces;
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
[MessagePackObject(AllowPrivate = true)]
[Union(0, typeof(ConditionNode))]
[Union(1, typeof(DelayNode))]
[Union(2, typeof(FileNode))]
[Union(3, typeof(HttpRequestNode))]
[Union(4, typeof(LoopNode))]
[Union(5, typeof(PreviewNode))]
[Union(6, typeof(SerializerNode))]
[Union(7, typeof(TriggerNode))]
[Union(8, typeof(VariableNode))]
public abstract partial class BuiltInNode : Node, INamedObject
{
    [YamlIgnore]
    [IgnoreMember]
    public abstract string Name { get; }

    [YamlIgnore]
    [IgnoreMember]
    public string? Description { get; set; }
}