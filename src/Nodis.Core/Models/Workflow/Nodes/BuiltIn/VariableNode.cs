using CommunityToolkit.Mvvm.Input;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class VariableNode : BuiltInNode
{
    [YamlIgnore]
    public override string Name => "Constant";

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
        NodeDataType.String,
        NodeDataType.Int64,
        NodeDataType.Double,
        NodeDataType.Boolean
    };

    [RelayCommand]
    private void AddConstant(NodeDataType dataType)
    {
        var index = 1;
        var namePrefix = dataType.ToString();
        string name;
        do name = $"{namePrefix} {index++}";
        while (Properties.Any(p => p.Name == name));
        var data = NodeData.CreateDefault(dataType);
        Properties.Add(new NodeProperty(name, data));
        DataOutputs.Add(new NodeDataOutputPin(name, data));
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