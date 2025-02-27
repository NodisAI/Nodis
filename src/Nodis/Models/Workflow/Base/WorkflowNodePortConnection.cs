using System.ComponentModel;

namespace Nodis.Models.Workflow;

public record WorkflowNodePortConnection
{
    public WorkflowNodeOutputPort OutputPort { get; }

    public WorkflowNodeInputPort InputPort { get; }

    public WorkflowNodePortConnection(WorkflowNodeOutputPort outputPort, WorkflowNodeInputPort inputPort)
    {
        if (outputPort.Owner == null) throw new ArgumentException("Output port must have an owner.", nameof(outputPort));
        if (inputPort.Owner == null) throw new ArgumentException("Input port must have an owner.", nameof(inputPort));

        OutputPort = outputPort;
        InputPort = inputPort;

        outputPort.PropertyChanged += HandleOutputPortPropertyChanged;
    }

    private void HandleOutputPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OutputPort.Value):
                InputPort.Value = OutputPort.Value;
                break;
            case nameof(OutputPort.HasValue):
                InputPort.HasValue = OutputPort.HasValue;
                break;
        }
    }
}