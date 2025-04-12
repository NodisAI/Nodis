using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial record NodePortConnection(int OutputNodeId, int OutputPinId, int InputNodeId, int InputPinId);

/// <summary>
/// Serializes and deserializes a <see cref="NodePortConnection"/> as a 4-element array.
/// </summary>
public class WorkflowNodePortConnectionYamlFormatter : IYamlFormatter<NodePortConnection>
{
    public void Serialize(ref Utf8YamlEmitter emitter, NodePortConnection value, YamlSerializationContext context)
    {
        emitter.BeginSequence();
        context.Serialize(ref emitter, value.OutputNodeId);
        context.Serialize(ref emitter, value.OutputPinId);
        context.Serialize(ref emitter, value.InputNodeId);
        context.Serialize(ref emitter, value.InputPinId);
        emitter.EndSequence();
    }

    public NodePortConnection Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        parser.ReadWithVerify(ParseEventType.SequenceStart);
        var outputNodeId = context.DeserializeWithAlias<int>(ref parser);
        var outputPinId = context.DeserializeWithAlias<int>(ref parser);
        var inputNodeId = context.DeserializeWithAlias<int>(ref parser);
        var inputPinId = context.DeserializeWithAlias<int>(ref parser);
        parser.ReadWithVerify(ParseEventType.SequenceEnd);

        return new NodePortConnection(outputNodeId, outputPinId, inputNodeId, inputPinId);
    }
}