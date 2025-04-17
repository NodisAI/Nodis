using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace Nodis.Frontend.Views;

public class ValueWithDescriptionInput : TemplatedControl
{
    public static readonly StyledProperty<ValueWithDescriptionBase> ValueWithDescriptionProperty =
        AvaloniaProperty.Register<ValueWithDescriptionInput, ValueWithDescriptionBase>(nameof(ValueWithDescription));

    public ValueWithDescriptionBase ValueWithDescription
    {
        get => GetValue(ValueWithDescriptionProperty);
        set => SetValue(ValueWithDescriptionProperty, value);
    }

    public static readonly DirectProperty<ValueWithDescriptionInput, bool> IsDataSupportedProperty =
        AvaloniaProperty.RegisterDirect<ValueWithDescriptionInput, bool>(nameof(IsDataSupported), o => o.IsDataSupported);

    public bool IsDataSupported =>
        VisualChildren.OfType<ContentPresenter>().FirstOrDefault()?.DataTemplates.Any(x => x.Match(ValueWithDescription)) ?? false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ValueWithDescriptionProperty) return;
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