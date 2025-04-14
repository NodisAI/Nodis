using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public readonly partial record struct NodePortConnection(ulong OutputNodeId, ulong OutputPinId, ulong InputNodeId, ulong InputPinId);

/// <summary>
/// Serializes and deserializes a <see cref="NodePortConnection"/> as a 4-element array.
/// </summary>
public class WorkflowNodePortConnectionYamlFormatter : IYamlFormatter<NodePortConnection>
{
    public void Serialize(ref Utf8YamlEmitter emitter, in NodePortConnection value, YamlSerializationContext context)
    {
        emitter.WriteString($"{value.OutputNodeId},{value.OutputPinId},{value.InputNodeId},{value.InputPinId}");
    }

    public NodePortConnection Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        var values = parser.ReadScalarAsString();
        if (string.IsNullOrEmpty(values) ||
            values.Split(',') is not { Length: 4 } parts ||
            !ulong.TryParse(parts[0], out var outputNodeId) ||
            !ulong.TryParse(parts[1], out var outputPinId) ||
            !ulong.TryParse(parts[2], out var inputNodeId) ||
            !ulong.TryParse(parts[3], out var inputPinId))
            throw new YamlParserException(parser.CurrentMark, "Invalid NodePortConnection format.");

        return new NodePortConnection(outputNodeId, outputPinId, inputNodeId, inputPinId);
    }
}