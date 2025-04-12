using MessagePack;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
[method: YamlConstructor]
public partial class NodeProperty(string name, NodeData data) : NodeMember(name)
{
    [YamlMember("data")]
    [Key(4)]
    public NodeData Data { get; } = data;

    /// <summary>
    /// MessagePack constructor for deserialization.
    /// </summary>
    internal NodeProperty() : this(string.Empty, NodeAnyData.Shared) { }
}