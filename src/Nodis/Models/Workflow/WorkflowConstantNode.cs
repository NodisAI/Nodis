using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.Input;
using IconPacks.Avalonia.EvaIcons;

namespace Nodis.Models.Workflow;

public enum WorkflowConstantNodeDataType
{
    String,
    Integer,
    Decimal,
    Boolean
}

public partial class WorkflowConstantNode : WorkflowNode
{
    public override string Name => "Constant";

    public override IEnumerable<WorkflowNodeMenuFlyoutItem> ContextMenuItems
    {
        get
        {
            foreach (var contextMenuItem in base.ContextMenuItems) yield return contextMenuItem;
            yield return WorkflowNodeMenuFlyoutItem.Separator;

            foreach (var dataType in (WorkflowConstantNodeDataType[])Enum.GetValues(typeof(WorkflowConstantNodeDataType)))
            {
                yield return new WorkflowNodeMenuFlyoutItem(
                    $"Add {dataType}",
                    dataType switch
                    {
                        WorkflowConstantNodeDataType.String => PackIconEvaIconsKind.Text,
                        WorkflowConstantNodeDataType.Integer => PackIconEvaIconsKind.Hash,
                        WorkflowConstantNodeDataType.Decimal => PackIconEvaIconsKind.Percent,
                        WorkflowConstantNodeDataType.Boolean => PackIconEvaIconsKind.Checkmark,
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
    private void AddConstant(WorkflowConstantNodeDataType dataType)
    {
        var index = 1;
        var namePrefix = dataType.ToString();
        string name;
        do name = $"{namePrefix} {index++}";
        while (Properties.Any(p => p.Name == name));
        ValueTuple<WorkflowNodeProperty, Type> pair = dataType switch
        {
            WorkflowConstantNodeDataType.String => (new WorkflowNodeStringProperty(name), typeof(string)),
            WorkflowConstantNodeDataType.Integer => (new WorkflowNodeIntegerProperty(name), typeof(int)),
            WorkflowConstantNodeDataType.Decimal => (new WorkflowNodeDecimalProperty(name), typeof(double)),
            WorkflowConstantNodeDataType.Boolean => (new WorkflowNodeBooleanProperty(name), typeof(bool)),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
        };
        Properties.Add(pair.Item1);
        Outputs.Add(new WorkflowNodeOutputPort(name, pair.Item2));
    }

    [RelayCommand]
    private void RemoveConstant(WorkflowNodeProperty property)
    {
        Properties.Remove(property);
        Outputs.Remove(Outputs.First(p => p.Name == property.Name));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}