using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class LoopNode : BuiltInNode
{
    [YamlIgnore]
    public override string Name => "Loop";

    public LoopNode()
    {
        ControlInput = new NodeControlInputPin();
        DataInputs.Add(new NodeDataInputPin("source", new NodeEnumerableData(new ArrayList())) { Description = "Enumerable data source" });
        DataInputs.Add(
            new NodeDataInputPin("linq", new NodeStringData(string.Empty))
            {
                Description = "Optional C# LINQ expression, e.g. `from item in source where item.Score > 80 select item.Name`"
            });
        ControlOutputs.Add(new NodeControlOutputPin("item") { Description = "Activates per iteration with current item" });
        ControlOutputs.Add(new NodeControlOutputPin("done") { Description = "Activates after final iteration" });
        DataOutputs.Add(
            new NodeDataOutputPin("current", new NodeAnyData())
            {
                Description = "Current item value (available in `item` context)"
            });
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}