using Nodis.Core.Extensions;

namespace Nodis.Core.Models.Workflow;

public sealed class StartNode : Node
{
    public override string Name => "Start";

    public StartNode()
    {
        Id = 1;
        ControlOutputs.Add(new ControlOutputPin());
    }

    public void Start()
    {
        ControlOutputs.First().To<ControlOutputPin>().Start();
        State = NodeStates.Completed;
    }

    public void Stop()
    {
        ControlOutputs.First().To<ControlOutputPin>().Stop();
        State = NodeStates.NotStarted;
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        // This node only notifies the next node to start, so it doesn't need to do anything
        throw new NotSupportedException();
    }

    private class ControlOutputPin() : NodeControlOutputPin("start")
    {
        public void Start() => CanExecute = true;
        public void Stop() => CanExecute = false;
    }
}