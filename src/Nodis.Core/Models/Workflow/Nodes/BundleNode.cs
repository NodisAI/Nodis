using MessagePack;
using Nodis.Core.Extensions;
using Nodis.Core.Interfaces;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Core.Models.Workflow;

/// <summary>
/// This node defined in a <see cref="InstalledBundle"/>
/// </summary>
[YamlObject]
[YamlObjectUnion("!mcp", typeof(BundleMcpNode))]
[MessagePackObject(AllowPrivate = true)]
[Union(0, typeof(BundleMcpNode))]
public abstract partial class BundleNode : Node, INamedObject
{
    [YamlMember("name")]
    [Key(12)]
    public required string Name { get; set; }

    [YamlMember("description")]
    [Key(13)]
    public string? Description { get; set; }

    /// <summary>
    /// The metadata of the bundle.
    /// </summary>
    [YamlMember("metadata")]
    [Key(14)]
    public required Metadata Metadata { get; init; }

    [YamlMember("runtime_id")]
    [Key(15)]
    public required string RuntimeId { get; init; }

    [YamlMember("data_inputs")]
    [Key(16)]
    public IReadOnlyList<NodeDataInputPin>? SerializableDataInputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataInputs.Reset(field);
        }
    }

    [YamlMember("data_outputs")]
    [Key(17)]
    public IReadOnlyList<NodeDataOutputPin>? SerializableDataOutputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataOutputs.Reset(field);
        }
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    public BundleNode Clone()
    {
        var options = ServiceLocator.Resolve<YamlSerializerOptions>();
        return YamlSerializer.Deserialize<BundleNode>(YamlSerializer.Serialize(this, options), options);
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class BundleMcpNode : BundleNode
{
    [YamlMember("tool_name")]
    [Key(18)]
    public required string ToolName { get; init; }
}