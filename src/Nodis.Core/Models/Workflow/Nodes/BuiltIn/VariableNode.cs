using CommunityToolkit.Mvvm.Input;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class VariableNode : BuiltInNode
{
    [YamlIgnore]
    public override string Name => "Variable";

    [YamlMember("items")]
    private IEnumerable<WorkflowConstantNodeYamlItem> YamlItems
    {
        get => Properties.Select(p => new WorkflowConstantNodeYamlItem(p.Name, p.Data));
        init
        {
            Properties.Clear();
            foreach (var item in value)
            {
                Properties.Add(new NodeProperty(item.Name, item.Data));
                DataOutputs.Add(new NodeDataOutputPin(item.Name, item.Data));
            }
        }
    }

    public static ReadOnlySpan<NodeDataType> SupportedDataTypes => new[]
    {
        NodeDataType.Boolean,
        NodeDataType.Int64,
        NodeDataType.Double,
        NodeDataType.String,
        NodeDataType.DateTime,
    };

    [RelayCommand]
    private void AddConstant(NodeDataType dataType)
    {
        var index = 1;
        var namePrefix = dataType.ToString();
        string name;
        do name = $"{namePrefix} {index++}";
        while (Properties.Any(p => p.Name == name));
        NodeData nodeData = dataType switch
        {
            NodeDataType.Boolean => new NodeBooleanData(false),
            NodeDataType.Int64 => new NodeInt64Data(0L),
            NodeDataType.Double => new NodeDoubleData(0d),
            NodeDataType.String => new NodeStringData(string.Empty),
            NodeDataType.DateTime => new NodeDateTimeData(DateTime.Now),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
        };
        Properties.Add(new NodeProperty(name, nodeData));
        DataOutputs.Add(new NodeDataOutputPin(name, nodeData));
    }

    [RelayCommand]
    private void RemoveConstant(NodeProperty property)
    {
        Properties.Remove(property);
        DataOutputs.Remove(DataOutputs.First(p => p.Name == property.Name));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}

[YamlObject]
// TODO: I hope VYaml can supports nested type serialization so that this can be well encapsulated
public partial record struct WorkflowConstantNodeYamlItem(string Name, NodeData Data);