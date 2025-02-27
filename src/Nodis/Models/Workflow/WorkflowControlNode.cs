using System.ComponentModel;
using System.Text.RegularExpressions;
using Nodis.Extensions;

namespace Nodis.Models.Workflow;

public class WorkflowConditionNode : WorkflowNode
{
    public override string Name => "Condition";

    public WorkflowConditionNode()
    {
        Inputs.Add(new WorkflowNodeInputPort("x", typeof(object)).HandlePropertyChanged(HandleConditionPropertyChanged));
        Inputs.Add(new WorkflowNodeInputPort("y", typeof(object)).HandlePropertyChanged(HandleConditionPropertyChanged));
        Outputs.Add(new WorkflowNodeOutputPort("true", typeof(bool)));
        Outputs.Add(new WorkflowNodeOutputPort("false", typeof(bool)));
    }

    private void HandleConditionPropertyChanged(WorkflowNodeInputPort sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkflowNodeInputPort.Connection)) return;
        var dataTypeX = Inputs[0].Connection?.DataType;
        var dataTypeY = Inputs[1].Connection?.DataType;
        if (dataTypeX != dataTypeY)
        {
            Properties.Reset(
            [
                new WorkflowNodeSelectionProperty("If",
                [
                    new ConditionValidator<object>("x Equals y", Equals)
                ]),
            ]);
        }
        else if (dataTypeX == typeof(string))
        {
            Properties.Reset(
            [
                new WorkflowNodeSelectionProperty("If",
                    [
                        new ConditionValidator<string>("x Equals y", (x, y) => x == y),
                        new ConditionValidator<string>("x Contains y", (x, y) => x.Contains(y)),
                        new ConditionValidator<string>("x Starts With y", (x, y) => x.StartsWith(y)),
                        new ConditionValidator<string>("x Ends With y", (x, y) => x.EndsWith(y)),
                        new ConditionValidator<string>("Regex y Matches x", Regex.IsMatch)
                    ]),
            ]);
        }
        else if (dataTypeX == typeof(int))
        {
            Properties.Reset(
            [
                new WorkflowNodeSelectionProperty("If",
                    [
                        new ConditionValidator<int>("x Equals y", (x, y) => x == y),
                        new ConditionValidator<int>("x Greater Than y", (x, y) => x > y),
                        new ConditionValidator<int>("x Less Than y", (x, y) => x < y),
                        new ConditionValidator<int>("x Greater Than Or Equals y", (x, y) => x >= y),
                        new ConditionValidator<int>("x Less Than Or Equals y", (x, y) => x <= y)
                    ]),
            ]);
        }
        else if (dataTypeX == typeof(double))
        {
            Properties.Reset(
            [
                new WorkflowNodeSelectionProperty("If",
                    [
                        // ReSharper disable once CompareOfFloatsByEqualityOperator
                        new ConditionValidator<double>("x Equals y", (x, y) => x == y),
                        new ConditionValidator<double>("x Approximates y", (x, y) => Math.Abs(x - y) < 0.0001),
                        new ConditionValidator<double>("x Greater Than y", (x, y) => x > y),
                        new ConditionValidator<double>("x Less Than y", (x, y) => x < y),
                        new ConditionValidator<double>("x Greater Than Or Equals y", (x, y) => x >= y),
                        new ConditionValidator<double>("x Less Than Or Equals y", (x, y) => x <= y)
                    ]),
            ]);
        }
        else if (dataTypeX == typeof(bool))
        {
            Properties.Reset(
            [
                new WorkflowNodeSelectionProperty("If",
                    [
                        new ConditionValidator<bool>("x Equals y", (x, y) => x == y)
                    ]),
            ]);
        }
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private class ConditionValidator<T>(string name, Func<T, T, bool> validator)
    {
        public override string ToString() => name;
    }
}

public class WorkflowLoopNode : WorkflowNode
{
    public override string Name => "Loop";

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}