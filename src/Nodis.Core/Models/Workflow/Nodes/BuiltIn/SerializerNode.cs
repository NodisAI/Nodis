using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using MessagePack;
using Nodis.Core.Extensions;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class SerializerNode : BuiltInNode
{
    [YamlIgnore]
    [IgnoreMember]
    public override string Name => "Serializer";

    public SerializerNode()
    {
        DataInputs.Add(new NodeDataInputPin("action", NodeEnumData.FromEnum<SerializerNodeAction>()));
        DataInputs.Add(new NodeDataInputPin("type", NodeEnumData.FromEnum<SerializerNodeType>()));
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
        return DataInputs["action"].Value?.ToString()?.ToEnum<SerializerNodeAction>() switch
        {
            SerializerNodeAction.Serialize => SerializeAsync(cancellationToken),
            SerializerNodeAction.Deserialize => DeserializeAsync(cancellationToken),
            _ => throw new NotSupportedException()
        };
    }

    private Task SerializeAsync(CancellationToken cancellationToken)
    {
        switch (DataInputs["type"].Value?.ToString()?.ToEnum<SerializerNodeType>())
        {
            case SerializerNodeType.Json:
            {
                var options = ServiceLocator.Resolve<JsonSerializerOptions>();
                DataOutputs["result"].Value = JsonSerializer.Serialize(DataInputs["data"].Value, options);
                break;
            }
            case SerializerNodeType.Xml when DataInputs["data"].Value is { } data:
            {
                using var ms = new MemoryStream();
                var xmlSerializer = new XmlSerializer(data.GetType());
                xmlSerializer.Serialize(ms, data);
                DataOutputs["result"].Value = Encoding.UTF8.GetString(ms.ToArray());
                break;
            }
            case SerializerNodeType.Yaml:
            {
                var options = ServiceLocator.Resolve<YamlSerializerOptions>();
                DataOutputs["result"].Value = YamlSerializer.Serialize(DataInputs["data"].Value, options);
                break;
            }
            case SerializerNodeType.Base64 when TryConvertToByteArray(DataInputs["data"].Value) is { } stream:
            {
                DataOutputs["result"].Value = Convert.ToBase64String(stream);
                break;
            }
            default:
            {
                throw new NotSupportedException();
            }
        }

        return Task.CompletedTask;

        static byte[]? TryConvertToByteArray(object? value)
        {
            return value switch
            {
                byte[] byteArray => byteArray,
                Memory<byte> memory => memory.ToArray(),
                string str => Encoding.UTF8.GetBytes(str),
                Stream { CanRead: true } stream => StreamToArray(stream),
                _ => null
            };
        }

        static byte[] StreamToArray(Stream stream)
        {
            var originalPosition = stream.Position;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            if (stream.CanSeek) stream.Position = originalPosition;
            return ms.ToArray();
        }
    }

    private async Task DeserializeAsync(CancellationToken cancellationToken)
    {
        switch (DataInputs["type"].Value?.ToString()?.ToEnum<SerializerNodeType>())
        {
            case SerializerNodeType.Json when DataInputs["data"].Value is Stream stream:
            {
                var options = ServiceLocator.Resolve<JsonSerializerOptions>();
                DataOutputs["result"].Value = await JsonSerializer.DeserializeAsync<IDictionary>(stream, options, cancellationToken);
                break;
            }
            case SerializerNodeType.Json when DataInputs["data"].Value is string json:
            {
                var options = ServiceLocator.Resolve<JsonSerializerOptions>();
                DataOutputs["result"].Value = JsonSerializer.Deserialize<IDictionary>(json, options);
                break;
            }
            case SerializerNodeType.Xml when DataInputs["data"].Value is Stream stream:
            {
                var xmlSerializer = new XmlSerializer(typeof(IDictionary));
                DataOutputs["result"].Value = xmlSerializer.Deserialize(stream);
                break;
            }
            case SerializerNodeType.Xml when DataInputs["data"].Value is string xml:
            {
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
                var xmlSerializer = new XmlSerializer(typeof(IDictionary));
                DataOutputs["result"].Value = xmlSerializer.Deserialize(ms);
                break;
            }
            case SerializerNodeType.Yaml when DataInputs["data"].Value is Stream stream:
            {
                var options = ServiceLocator.Resolve<YamlSerializerOptions>();
                DataOutputs["result"].Value = await YamlSerializer.DeserializeAsync<IDictionary>(stream, options);
                break;
            }
            case SerializerNodeType.Yaml when DataInputs["data"].Value is string yaml:
            {
                var options = ServiceLocator.Resolve<YamlSerializerOptions>();
                DataOutputs["result"].Value = YamlSerializer.Deserialize<IDictionary>(Encoding.UTF8.GetBytes(yaml), options);
                break;
            }
            case SerializerNodeType.Base64 when DataInputs["data"].Value is string base64:
            {
                var bytes = Convert.FromBase64String(base64);
                DataOutputs["result"].Value = Encoding.UTF8.GetString(bytes);
                break;
            }
            default:
            {
                throw new NotSupportedException();
            }
        }
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