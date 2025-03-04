using System.ComponentModel;
using System.Text.RegularExpressions;
using Nodis.Extensions;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowConditionNode : WorkflowNode
{
    [YamlIgnore]
    public override string Name => "Condition";

    public WorkflowConditionNode()
    {
        ControlInput = new WorkflowNodeControlInputPort();
        ControlOutputs.Add(new WorkflowNodeControlOutputPort("True"));
        ControlOutputs.Add(new WorkflowNodeControlOutputPort("False"));
        DataInputs.Add(new WorkflowNodeDataInputPort("x", new WorkflowNodeMutableData(), true).HandlePropertyChanged(HandleConditionPropertyChanged));
        DataInputs.Add(new WorkflowNodeDataInputPort("y", new WorkflowNodeMutableData(), true).HandlePropertyChanged(HandleConditionPropertyChanged));
    }

    private void HandleConditionPropertyChanged(WorkflowNodeDataInputPort sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkflowNodeDataInputPort.Connection)) return;

        var (portX, portY) = (DataInputs[0], DataInputs[1]);
        WorkflowNodeDataType dataTypeX, dataTypeY;
        if (portX.Connection != null && portY.Connection != null)
        {
            dataTypeX = portX.Connection.Data.Type;
            dataTypeY = portY.Connection.Data.Type;
        }
        else if (portX.Connection == null && portY.Connection == null)
        {
            dataTypeX = dataTypeY = WorkflowNodeDataType.Any;
        }
        else if (portX.Connection != null)
        {
            dataTypeX = dataTypeY = portX.Connection.Data.Type;
        }
        else // portY.Connection != null
        {
            dataTypeX = dataTypeY = portY.Connection!.Data.Type;
        }

        portX.Data.To<WorkflowNodeMutableData>().MutateType(dataTypeX);
        portY.Data.To<WorkflowNodeMutableData>().MutateType(dataTypeY);

        IList validators = dataTypeX == dataTypeY ? dataTypeX switch
        {
            WorkflowNodeDataType.Integer => new List<ConditionValidator<int>>
            {
                new("x Equals y", (x, y) => x == y),
                new("x Greater Than y", (x, y) => x > y),
                new("x Less Than y", (x, y) => x < y),
                new("x Greater Than Or Equals y", (x, y) => x >= y),
                new("x Less Than Or Equals y", (x, y) => x <= y)
            },
            WorkflowNodeDataType.Float => new List<ConditionValidator<float>>
            {
                new("x Equals y", (x, y) => Math.Abs(x - y) < float.Epsilon),
                new("x Greater Than y", (x, y) => x > y),
                new("x Less Than y", (x, y) => x < y),
                new("x Greater Than Or Equals y", (x, y) => x >= y),
                new("x Less Than Or Equals y", (x, y) => x <= y)
            },
            WorkflowNodeDataType.Text => new List<ConditionValidator<string>>
            {
                new("x Equals y", (x, y) => x == y),
                new("x Contains y", (x, y) => x.Contains(y)),
                new("x Starts With y", (x, y) => x.StartsWith(y)),
                new("x Ends With y", (x, y) => x.EndsWith(y)),
                new("Regex y Matches x", Regex.IsMatch)
            },
            WorkflowNodeDataType.DateTime => new List<ConditionValidator<DateTime>>
            {
                new("x Equals y", (x, y) => x == y),
                new("x Greater Than y", (x, y) => x > y),
                new("x Less Than y", (x, y) => x < y),
                new("x Greater Than Or Equals y", (x, y) => x >= y),
                new("x Less Than Or Equals y", (x, y) => x <= y)
            },
            WorkflowNodeDataType.List => new List<ConditionValidator<IList>>
            {
                new("x Equals y", (x, y) => x.Cast<object?>().SequenceEqual(y.Cast<object?>()))
            },
            _ => new List<ConditionValidator<object>> { new("x Equals y", Equals) }
        } : new List<ConditionValidator<object>> { new("x Equals y", Equals) };

        Properties.Reset([new WorkflowNodeProperty("If", new WorkflowNodeListData(validators))]);
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        if (Properties.Count == 0) return Task.CompletedTask;

        var result = Properties[0].Data.To<WorkflowNodeListData>().SelectedItem.As<IConditionValidator>()
            ?.Validate(DataInputs[0].Value, DataInputs[1].Value) is true;
        ControlOutputs[0].CanExecute = result;
        ControlOutputs[1].CanExecute = !result;
        return Task.CompletedTask;
    }

    private interface IConditionValidator
    {
        bool Validate(object? x, object? y);
    }

    private class ConditionValidator<T>(string name, Func<T, T, bool> validator) : IConditionValidator
    {
        public override string ToString() => name;

        public bool Validate(object? x, object? y)
        {
            if (x is not T xValue || y is not T yValue) return false;
            return validator(xValue, yValue);
        }
    }
}