using Nodis.Core.Extensions;
using Nodis.Core.Interfaces;

namespace Nodis.Core.Models.Workflow;

public sealed class StartNode : Node, INamedObject
{
    public string Name => "Start";

    public string? Description => "The start node of the workflow. It is the first node that is executed when the workflow is started.";

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