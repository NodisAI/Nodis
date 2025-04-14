using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace Nodis.Frontend.Views;

public class NodeDataInput : TemplatedControl
{
    public static readonly StyledProperty<NodeData> DataProperty =
        AvaloniaProperty.Register<NodeDataInput, NodeData>(nameof(Data));

    public NodeData Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DirectProperty<NodeDataInput, bool> IsDataSupportedProperty =
        AvaloniaProperty.RegisterDirect<NodeDataInput, bool>(nameof(IsDataSupported), o => o.IsDataSupported);

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