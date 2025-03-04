using CommunityToolkit.Mvvm.ComponentModel;
using Nodis.Extensions;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

/// <summary>
/// <see cref="WorkflowNodeData"/> is the base class for all data types
/// that can be stored in a <see cref="WorkflowNodeProperty"/> or <see cref="WorkflowNodePin"/>.
/// It defines the <see cref="Value"/> property that stores the actual data, and a corresponding <see cref="Type"/> property.
/// </summary>
/// <remarks>
/// <see cref="Value"/> is always the actual type, or null. <see cref="ErrorMessage"/> indicates if there is an error.
/// </remarks>
[YamlObject]
[YamlObjectUnion("!bool", typeof(WorkflowNodeBooleanData))]
[YamlObjectUnion("!int", typeof(WorkflowNodeIntegerData))]
[YamlObjectUnion("!float", typeof(WorkflowNodeFloatData))]
[YamlObjectUnion("!str", typeof(WorkflowNodeTextData))]
[YamlObjectUnion("!datetime", typeof(WorkflowNodeDateTimeData))]
public abstract partial class WorkflowNodeData : ObservableObject
{
    [YamlIgnore]
    public abstract WorkflowNodeDataType Type { get; }

    /// <summary>
    /// We use object here so that type conversion can be done between two data ports.
    /// </summary>
    [YamlMember("value")]
    public object? Value
    {
        get;
        set
        {
            if (Equals(field, value)) return;
            try
            {
                field = ConvertValue(value);
            }
            catch (Exception e)
            {
                ErrorMessage = e.GetFriendlyMessage();
            }
            if (ErrorMessage != null) return;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [YamlIgnore]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// Try to convert the value to the correct type.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="Exception">Throw an exception if the value cannot be converted.</exception>
    public virtual object? ConvertValue(object? value) => value;

    public static WorkflowNodeData CreateDefault(WorkflowNodeDataType type)
    {
        return type switch
        {
            WorkflowNodeDataType.Boolean => new WorkflowNodeBooleanData { Value = false },
            WorkflowNodeDataType.Integer => new WorkflowNodeIntegerData { Value = 0 },
            WorkflowNodeDataType.Float => new WorkflowNodeFloatData { Value = 0f },
            WorkflowNodeDataType.Text => new WorkflowNodeTextData { Value = string.Empty },
            WorkflowNodeDataType.DateTime => new WorkflowNodeDateTimeData { Value = DateTime.Now },
            WorkflowNodeDataType.List => new WorkflowNodeListData(Array.Empty<object?>()),
            WorkflowNodeDataType.Dictionary => new WorkflowNodeDictionaryData(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}

[YamlObject]
public partial class WorkflowNodeBooleanData : WorkflowNodeData
{
    [YamlIgnore]
    public override WorkflowNodeDataType Type => WorkflowNodeDataType.Boolean;
}

[YamlObject]
public partial class WorkflowNodeIntegerData : WorkflowNodeData
{
    [YamlIgnore]
    public override WorkflowNodeDataType Type => WorkflowNodeDataType.Integer;

    [YamlMember("min")]
    public int Min { get; init; } = int.MinValue;

    [YamlMember("max")]
    public int Max { get; init; } = int.MaxValue;

    public override object ConvertValue(object? value)
    {
        if (value is not IConvertible convertible) throw new FormatException("Value is not an integer, nor convertible to an integer.");
        var intValue = convertible.ToInt32(null);
        if (intValue < Min) throw new FormatException($"Value must be greater than or equal to {Min}.");
        if (intValue > Max) throw new FormatException($"Value must be less than or equal to {Max}.");
        return intValue;
    }
}

[YamlObject]
public partial class WorkflowNodeFloatData : WorkflowNodeData
{
    [YamlIgnore]
    public override WorkflowNodeDataType Type => WorkflowNodeDataType.Float;

    [YamlMember("min")]
    public float Min { get; init; } = float.NegativeInfinity;

    [YamlMember("max")]
    public float Max { get; init; } = float.PositiveInfinity;

    [YamlMember("precision")]
    public float Precision { get; init; } = 0.1f;

    public override object ConvertValue(object? value)
    {
        if (value is not IConvertible convertible) throw new FormatException("Value is not a decimal, nor convertible to a decimal.");
        var floatValue = convertible.ToSingle(null);
        if (floatValue < Min) throw new FormatException($"Value must be greater than or equal to {Min}.");
        if (floatValue > Max) throw new FormatException($"Value must be less than or equal to {Max}.");
        floatValue = (float)Math.Round((double)floatValue / Precision) * Precision;
        return floatValue;
    }
}

[YamlObject]
public partial class WorkflowNodeTextData : WorkflowNodeData
{
    [YamlIgnore]
    public override WorkflowNodeDataType Type => WorkflowNodeDataType.Text;
}

[YamlObject]
public partial class WorkflowNodeDateTimeData : WorkflowNodeData
{
    [YamlIgnore]
    public override WorkflowNodeDataType Type => WorkflowNodeDataType.DateTime;

    [YamlMember("min")]
    public DateTime Min { get; init; } = DateTime.MinValue;

    [YamlMember("max")]
    public DateTime Max { get; init; } = DateTime.MaxValue;

    public override object ConvertValue(object? value)
    {
        if (value is not IConvertible convertible) throw new FormatException("Value is not a DateTime, nor convertible to a DateTime.");
        var dateTimeValue = convertible.ToDateTime(null);
        if (dateTimeValue < Min) throw new FormatException($"Value must be greater than or equal to {Min}.");
        if (dateTimeValue > Max) throw new FormatException($"Value must be less than or equal to {Max}.");
        return dateTimeValue;
    }
}

[YamlObject]
public partial class WorkflowNodeListData : WorkflowNodeData
{
    [YamlIgnore]
    public override WorkflowNodeDataType Type => WorkflowNodeDataType.List;

    public WorkflowNodeListData(IList items)
    {
        Value = items;
    }

    [YamlConstructor]
    private WorkflowNodeListData() { }

    [ObservableProperty]
    [YamlMember("selected_index")]
    public partial int SelectedIndex { get; set; }

    [YamlIgnore]
    public object? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < Value.To<IList>()!.Count ? Value.To<IList>()![SelectedIndex] : null;
}

public partial class WorkflowNodeDictionaryData : WorkflowNodeData
{
    public override WorkflowNodeDataType Type => WorkflowNodeDataType.Dictionary;

    [ObservableProperty]
    public partial KeyValuePair<string, object>? SelectedItem { get; set; }
}

public class WorkflowNodeMutableData(WorkflowNodeDataType type = WorkflowNodeDataType.Any) : WorkflowNodeData
{
    public override WorkflowNodeDataType Type => type;

    private WorkflowNodeDataType type = type;

    public void MutateType(WorkflowNodeDataType newType) => SetProperty(ref type, newType, nameof(Type));
}