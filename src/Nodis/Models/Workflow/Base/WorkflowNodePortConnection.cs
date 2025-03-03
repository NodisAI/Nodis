using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace Nodis.Models.Workflow;

[YamlObject]
public readonly partial record struct WorkflowNodePortConnection(int OutputNodeId, int OutputPortId, int InputNodeId, int InputPortId);

/// <summary>
/// Serializes and deserializes a <see cref="WorkflowNodePortConnection"/> as a 4-element array.
/// </summary>
internal class WorkflowNodePortConnectionYamlFormatter : IYamlFormatter<WorkflowNodePortConnection>
{
    public void Serialize(ref Utf8YamlEmitter emitter, WorkflowNodePortConnection value, YamlSerializationContext context)
    {
        emitter.BeginSequence();
        context.Serialize(ref emitter, value.OutputNodeId);
        context.Serialize(ref emitter, value.OutputPortId);
        context.Serialize(ref emitter, value.InputNodeId);
        context.Serialize(ref emitter, value.InputPortId);
        emitter.EndSequence();
    }

    public WorkflowNodePortConnection Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        parser.ReadWithVerify(ParseEventType.SequenceStart);
        var outputNodeId = context.DeserializeWithAlias<int>(ref parser);
        var outputPortId = context.DeserializeWithAlias<int>(ref parser);
        var inputNodeId = context.DeserializeWithAlias<int>(ref parser);
        var inputPortId = context.DeserializeWithAlias<int>(ref parser);
        parser.ReadWithVerify(ParseEventType.SequenceEnd);

        return new WorkflowNodePortConnection(outputNodeId, outputPortId, inputNodeId, inputPortId);
    }
}