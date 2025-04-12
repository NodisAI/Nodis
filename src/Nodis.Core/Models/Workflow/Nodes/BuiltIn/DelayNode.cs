using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class DelayNode : BuiltInNode
{
    [YamlIgnore]
    public override string Name => "Delay";

    public DelayNode()
    {
        ControlInput = new NodeControlInputPin();
        DataInputs.Add(new NodeDataInputPin("duration", new NodeDoubleData(1f)) { Description = "in seconds" });
        ControlOutputs.Add(new NodeControlOutputPin("then") { Description = "Activates after specified duration" });
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds((float?)DataInputs["duration"].Value ?? 0f), cancellationToken);
}