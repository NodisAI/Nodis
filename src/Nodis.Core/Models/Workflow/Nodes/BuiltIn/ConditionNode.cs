using Nodis.Core.Extensions;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class ConditionNode : BuiltInNode
{
    [YamlIgnore]
    public override string Name => "Condition";

    public ConditionNode()
    {
        ControlInput = new NodeControlInputPin();
        IsDataInputsDynamic = true;
        // DataInputs.Add(new NodeDataInputPin("x", new NodeAnyData()).HandlePropertyChanged(HandleConditionPropertyChanged));
        // DataInputs.Add(new NodeDataInputPin("y", new NodeAnyData()).HandlePropertyChanged(HandleConditionPropertyChanged));
        ControlOutputs.Add(new NodeControlOutputPin("true") { Description = "Activates when condition evaluates true" });
        ControlOutputs.Add(new NodeControlOutputPin("false") { Description = "Activates when condition evaluates false" });
        Properties.Add(new NodeProperty("condition", new NodeStringData(string.Empty))
        {
            Description = "C# boolean expression using dynamic inputs above, e.g. `x.Contains(y)`"
        });
    }

    // private void HandleConditionPropertyChanged(NodeDataInputPin sender, PropertyChangedEventArgs e)
    // {
    //     if (e.PropertyName != nameof(NodeDataInputPin.Connection)) return;
    //
    //     var (portX, portY) = (DataInputs[0], DataInputs[1]);
    //     NodeDataType dataTypeX, dataTypeY;
    //     if (portX.Connection != null && portY.Connection != null)
    //     {
    //         dataTypeX = portX.Connection.Data.Type;
    //         dataTypeY = portY.Connection.Data.Type;
    //     }
    //     else if (portX.Connection == null && portY.Connection == null)
    //     {
    //         dataTypeX = dataTypeY = NodeDataType.Object;
    //     }
    //     else if (portX.Connection != null)
    //     {
    //         dataTypeX = dataTypeY = portX.Connection.Data.Type;
    //     }
    //     else // portY.Connection != null
    //     {
    //         dataTypeX = dataTypeY = portY.Connection!.Data.Type;
    //     }
    //
    //     // portX.Data.To<NodeAnyData>().MutateType(dataTypeX);
    //     // portY.Data.To<NodeAnyData>().MutateType(dataTypeY);
    //
    //     IList validators = dataTypeX == dataTypeY ? dataTypeX switch
    //     {
    //         NodeDataType.Int64 => new List<ConditionValidator<int>>
    //         {
    //             new("x Equals y", (x, y) => x == y),
    //             new("x Greater Than y", (x, y) => x > y),
    //             new("x Less Than y", (x, y) => x < y),
    //             new("x Greater Than Or Equals y", (x, y) => x >= y),
    //             new("x Less Than Or Equals y", (x, y) => x <= y)
    //         },
    //         NodeDataType.Double => new List<ConditionValidator<float>>
    //         {
    //             new("x Equals y", (x, y) => Math.Abs(x - y) < float.Epsilon),
    //             new("x Greater Than y", (x, y) => x > y),
    //             new("x Less Than y", (x, y) => x < y),
    //             new("x Greater Than Or Equals y", (x, y) => x >= y),
    //             new("x Less Than Or Equals y", (x, y) => x <= y)
    //         },
    //         NodeDataType.String => new List<ConditionValidator<string>>
    //         {
    //             new("x Equals y", (x, y) => x == y),
    //             new("x Contains y", (x, y) => x.Contains(y)),
    //             new("x Starts With y", (x, y) => x.StartsWith(y)),
    //             new("x Ends With y", (x, y) => x.EndsWith(y)),
    //             new("Regex y Matches x", Regex.IsMatch)
    //         },
    //         NodeDataType.DateTime => new List<ConditionValidator<DateTime>>
    //         {
    //             new("x Equals y", (x, y) => x == y),
    //             new("x Greater Than y", (x, y) => x > y),
    //             new("x Less Than y", (x, y) => x < y),
    //             new("x Greater Than Or Equals y", (x, y) => x >= y),
    //             new("x Less Than Or Equals y", (x, y) => x <= y)
    //         },
    //         NodeDataType.Enumerable => new List<ConditionValidator<IList>>
    //         {
    //             new("x Equals y", (x, y) => x.Cast<object?>().SequenceEqual(y.Cast<object?>()))
    //         },
    //         _ => new List<ConditionValidator<object>> { new("x Equals y", Equals) }
    //     } : new List<ConditionValidator<object>> { new("x Equals y", Equals) };
    //
    //     Properties.Reset([new NodeProperty("If", new NodeEnumerableData(validators))]);
    // }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        if (Properties.Count == 0) return Task.CompletedTask;

        var result = Properties[0].Data.To<NodeEnumerableData>().SelectedItem.As<IConditionValidator>()
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