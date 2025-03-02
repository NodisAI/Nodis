using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Nodis.Models.Workflow;

namespace Nodis.Views.Workflow;

public class WorkflowNodeDataInput : TemplatedControl
{
    public static readonly StyledProperty<WorkflowNodeData> DataProperty =
        AvaloniaProperty.Register<WorkflowNodeDataInput, WorkflowNodeData>(nameof(Data));

    public WorkflowNodeData Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DirectProperty<WorkflowNodeDataInput, bool> IsDataSupportedProperty =
        AvaloniaProperty.RegisterDirect<WorkflowNodeDataInput, bool>(nameof(IsDataSupported), o => o.IsDataSupported);

    public bool IsDataSupported =>
        VisualChildren.OfType<ContentPresenter>().FirstOrDefault()?.DataTemplates.Any(x => x.Match(Data)) ?? false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != DataProperty) return;
        RaiseIsDataSupportedPropertyChanged();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        RaiseIsDataSupportedPropertyChanged();
    }

    private void RaiseIsDataSupportedPropertyChanged()
    {
        var isDataTypeSupported = IsDataSupported;
        RaisePropertyChanged(IsDataSupportedProperty, !isDataTypeSupported, isDataTypeSupported);
    }
}