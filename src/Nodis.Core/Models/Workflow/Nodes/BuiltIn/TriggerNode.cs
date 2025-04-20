using System.Runtime.Serialization;
using MessagePack;
using Nodis.Core.Extensions;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class TriggerNode : BuiltInNode
{
    [YamlIgnore]
    [IgnoreMember]
    public override string Name => "Trigger";

    public TriggerNode()
    {
        DataInputs.Add(new NodeDataInputPin("type", NodeEnumData.FromEnum<TriggerNodeType>()));
        DataInputs.Add(
            new NodeDataInputPin("interval", new NodeDoubleData(1f))
            {
                Description = "Interval in seconds",
                Condition = new NodePinValueCondition(
                    "type",
                    d => d.Value?.ToString()?.ToEnum<TriggerNodeType>() is TriggerNodeType.Timer)
            });
        DataInputs.Add(
            new NodeDataInputPin(
                "hotkey",
                new NodeStringData(string.Empty)
                {
                    Watermark = "e.g. Ctrl + Shift + I",
                })
            {
                Description = "A global hotkey to trigger the node",
                Condition = new NodePinValueCondition("type",
                    d => d.Value?.ToString()?.ToEnum<TriggerNodeType>() is TriggerNodeType.Hotkey)
            });
        ControlOutputs.Add(
            new NodeControlOutputPin("then")
            {
                Description = "Activates when triggered"
            });
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public enum TriggerNodeType
{
    [EnumMember(Value = "timer")]
    Timer,
    [EnumMember(Value = "hotkey")]
    Hotkey,
    // [EnumMember(Value = "webhook")]
    // Webhook,
}