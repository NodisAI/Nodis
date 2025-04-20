using System.Text.Json;
using MessagePack;
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
public abstract partial class BundleNode : Node
{
    [YamlIgnore]
    [IgnoreMember]
    public override string Name =>
        throw new InvalidOperationException("Should implemented in derived class");

    /// <summary>
    /// The metadata of the bundle.
    /// </summary>
    [YamlMember("metadata")]
    [Key(12)]
    public required Metadata Metadata { get; init; }

    [YamlMember("runtime_id")]
    [Key(13)]
    public required string RuntimeId { get; init; }

    [YamlMember("data_inputs")]
    [Key(14)]
    public IList<NodeDataInputPin>? SerializableDataInputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataInputs.AddRange(field);
        }
    }

    [YamlMember("data_outputs")]
    [Key(15)]
    public IList<NodeDataOutputPin>? SerializableDataOutputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataOutputs.AddRange(field);
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
    [YamlMember("name")]
    [Key(16)]
    public required new string Name { get; init; }

    [YamlMember("tool_name")]
    [Key(17)]
    public required string ToolName { get; init; }

    public static BundleMcpNode Create(string name, string toolName, JsonElement inputSchema)
    {
        throw new NotImplementedException();
    }
}