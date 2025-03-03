using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using IconPacks.Avalonia.EvaIcons;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowConstantNode : WorkflowNode
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
                Properties.Add(new WorkflowNodeProperty(item.Name, item.Data));
                DataOutputs.Add(new WorkflowNodeDataOutputPort(item.Name, item.Data));
            }
        }
    }

    private static ReadOnlySpan<WorkflowNodeDataType> SupportedDataTypes => new[]
    {
        WorkflowNodeDataType.Text,
        WorkflowNodeDataType.Integer,
        WorkflowNodeDataType.Float,
        WorkflowNodeDataType.Boolean
    };

    [YamlIgnore]
    public override IEnumerable<WorkflowNodeMenuFlyoutItem> ContextMenuItems
    {
        get
        {
            foreach (var contextMenuItem in base.ContextMenuItems) yield return contextMenuItem;
            yield return WorkflowNodeMenuFlyoutItem.Separator;

            for (var i = 0; i < SupportedDataTypes.Length; i++)
            {
                var dataType = SupportedDataTypes[i];
                yield return new WorkflowNodeMenuFlyoutItem(
                    $"Add {dataType}",
                    dataType switch
                    {
                        WorkflowNodeDataType.Text => PackIconEvaIconsKind.Text,
                        WorkflowNodeDataType.Integer => PackIconEvaIconsKind.Hash,
                        WorkflowNodeDataType.Float => PackIconEvaIconsKind.Percent,
                        WorkflowNodeDataType.Boolean => PackIconEvaIconsKind.Checkmark,
                        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
                    },
                    AddConstantCommand,
                    dataType);
            }

            if (Properties.Count == 0) yield break;
            yield return WorkflowNodeMenuFlyoutItem.Separator;
            foreach (var property in Properties)
            {
                yield return new WorkflowNodeMenuFlyoutItem(
                    $"Remove {property.Name}",
                    PackIconEvaIconsKind.Trash2,
                    RemoveConstantCommand,
                    property,
                    NotificationType.Error);
            }
        }
    }

    [RelayCommand]
    private void AddConstant(WorkflowNodeDataType dataType)
    {
        var index = 1;
        var namePrefix = dataType.ToString();
        string name;
        do name = $"{namePrefix} {index++}";
        while (Properties.Any(p => p.Name == name));
        var data = WorkflowNodeData.CreateDefault(dataType);
        Properties.Add(new WorkflowNodeProperty(name, data));
        DataOutputs.Add(new WorkflowNodeDataOutputPort(name, data));
    }

    [RelayCommand]
    private void RemoveConstant(WorkflowNodeProperty property)
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
public partial record struct WorkflowConstantNodeYamlItem(string Name, WorkflowNodeData Data);