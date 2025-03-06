namespace Nodis.Models.Workflow;

public class WorkflowDisplayNode : WorkflowBuiltInNode
{
    public override string Name => "Display";

    public override object? FooterContent => DataInputs[0].Data.Value;

    public WorkflowDisplayNode()
    {
        ControlInput = new WorkflowNodeControlInputPin();
        DataInputs.Add(new WorkflowNodeDataInputPin("data", new WorkflowNodeMutableData()));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        OnPropertyChanged(nameof(FooterContent));
        return Task.CompletedTask;
    }
}