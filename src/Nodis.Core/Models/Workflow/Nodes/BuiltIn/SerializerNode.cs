using System.Runtime.Serialization;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class SerializerNode : BuiltInNode
{
    [YamlIgnore]
    public override string Name => "Serializer";

    public SerializerNode()
    {
        DataInputs.Add(new NodeDataInputPin("action", new NodeEnumData(typeof(SerializerNodeAction))));
        DataInputs.Add(new NodeDataInputPin("type", new NodeEnumData(typeof(SerializerNodeType))));
        DataInputs.Add(new NodeDataInputPin("data", new NodeAnyData())
        {
            Description = "Data to serialize/deserialize"
        });
        ControlOutputs.Add(new NodeControlOutputPin("success"));
        ControlOutputs.Add(new NodeControlOutputPin("failure"));
        DataOutputs.Add(new NodeDataOutputPin("result", new NodeAnyData())
        {
            Description = "Serialized/Deserialized data"
        });
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public enum SerializerNodeAction
{
    [EnumMember(Value = "serialize")]
    Serialize,
    [EnumMember(Value = "deserialize")]
    Deserialize
}

public enum SerializerNodeType
{
    [EnumMember(Value = "json")]
    Json,
    [EnumMember(Value = "xml")]
    Xml,
    [EnumMember(Value = "yaml")]
    Yaml,
    [EnumMember(Value = "base64")]
    Base64
}