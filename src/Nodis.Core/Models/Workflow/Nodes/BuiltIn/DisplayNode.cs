namespace Nodis.Core.Models.Workflow;

public class DisplayNode : BuiltInNode
{
    public override string Name => "Display";

    public override object? FooterContent => DataInputs[0].Data.Value;

    public DisplayNode()
    {
        ControlInput = new NodeControlInputPin();
        DataInputs.Add(new NodeDataInputPin("data", new NodeAnyData()));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        OnPropertyChanged(nameof(FooterContent));
        return Task.CompletedTask;
    }
}