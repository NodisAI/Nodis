using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePack;
using Nodis.Core.Extensions;
using Nodis.Core.Networking;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

/// <summary>
/// <see cref="NodeData"/> is the base class for all data types
/// that can be stored in a <see cref="NodeProperty"/> or <see cref="NodePin"/>.
/// It defines the <see cref="Value"/> property that stores the actual data, and a corresponding <see cref="Type"/> property.
/// </summary>
/// <remarks>
/// <see cref="Value"/> is always the actual type, or null. <see cref="ErrorMessage"/> indicates if there is an error.
/// </remarks>
[YamlObject]
[YamlObjectUnion("!any", typeof(NodeAnyData))]
[YamlObjectUnion("!bool", typeof(NodeBooleanData))]
[YamlObjectUnion("!int", typeof(NodeInt64Data))]
[YamlObjectUnion("!float", typeof(NodeDoubleData))]
[YamlObjectUnion("!str", typeof(NodeStringData))]
[YamlObjectUnion("!datetime", typeof(NodeDateTimeData))]
[YamlObjectUnion("!enum", typeof(NodeEnumData))]
[YamlObjectUnion("!seq", typeof(NodeEnumerableData))]
[YamlObjectUnion("!map", typeof(NodeDictionaryData))]
[YamlObjectUnion("!bin", typeof(NodeStreamData))]
[MessagePackObject]
[Union(0, typeof(NodeAnyData))]
[Union(1, typeof(NodeBooleanData))]
[Union(2, typeof(NodeInt64Data))]
[Union(3, typeof(NodeDoubleData))]
[Union(4, typeof(NodeStringData))]
[Union(5, typeof(NodeDateTimeData))]
[Union(6, typeof(NodeEnumData))]
[Union(7, typeof(NodeEnumerableData))]
[Union(8, typeof(NodeDictionaryData))]
[Union(9, typeof(NodeStreamData))]
public abstract partial class NodeData : ObservableObject
{
    [IgnoreMember]
    private readonly NetworkObjectTracker tracker;

    [YamlIgnore]
    [Key(0)]
    public Guid NetworkObjectId
    {
        get => tracker.Id;
        protected set => tracker.Id = value;
    }

    [YamlIgnore]
    [IgnoreMember]
    public abstract NodeDataType Type { get; }

    /// <summary>
    /// We use object here so that type conversion can be done between two data ports.
    /// </summary>
    [YamlMember("value")]
    [Key(1)]
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
    [Key(2)]
    public partial string? ErrorMessage { get; internal set; }

    protected NodeData()
    {
        tracker = new NetworkObjectTracker(this);
    }

    /// <summary>
    /// Try to convert the value to the correct type.
    /// </summary>
    /// <param name="value"></param>
    /// <exception cref="Exception">Throw an exception if the value cannot be converted.</exception>
    public virtual object? ConvertValue(object? value) => value;
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeAnyData : NodeData
{
    public static NodeAnyData Shared { get; } = new();

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Object;
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeBooleanData : NodeData
{
    public NodeBooleanData(bool value)
    {
        Value = value;
    }

    [YamlConstructor]
    private NodeBooleanData() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Boolean;

    public override object ConvertValue(object? value)
    {
        if (value is not IConvertible convertible) throw new FormatException("Value is not a boolean, nor convertible to a boolean.");
        return convertible.ToBoolean(null);
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeInt64Data : NodeData
{
    public NodeInt64Data(long value)
    {
        Value = value;
    }

    [YamlConstructor]
    private NodeInt64Data() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Int64;

    [ObservableProperty]
    [YamlMember("min")]
    [Key(3)]
    public partial long Min { get; set; } = long.MinValue;

    [ObservableProperty]
    [YamlMember("max")]
    [Key(4)]
    public partial long Max { get; set; } = long.MaxValue;

    public override object ConvertValue(object? value)
    {
        if (value is not IConvertible convertible) throw new FormatException("Value is not an integer, nor convertible to an integer.");
        var int64Value = convertible.ToInt64(null);
        if (int64Value < Min) throw new FormatException($"Value must be greater than or equal to {Min}.");
        if (int64Value > Max) throw new FormatException($"Value must be less than or equal to {Max}.");
        return int64Value;
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeDoubleData : NodeData
{
    public NodeDoubleData(double value)
    {
        Value = value;
    }

    [YamlConstructor]
    private NodeDoubleData() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Double;

    [ObservableProperty]
    [YamlMember("min")]
    [Key(3)]
    public partial double Min { get; set; } = double.NegativeInfinity;

    [ObservableProperty]
    [YamlMember("max")]
    [Key(4)]
    public partial double Max { get; set; } = double.PositiveInfinity;

    [ObservableProperty]
    [YamlMember("precision")]
    [Key(5)]
    public partial double Precision { get; set; } = 0.1d;

    public override object ConvertValue(object? value)
    {
        if (value is not IConvertible convertible) throw new FormatException("Value is not a decimal, nor convertible to a decimal.");
        var doubleValue = convertible.ToDouble(null);
        if (doubleValue < Min) throw new FormatException($"Value must be greater than or equal to {Min}.");
        if (doubleValue > Max) throw new FormatException($"Value must be less than or equal to {Max}.");
        doubleValue = Math.Round(doubleValue / Precision) * Precision;
        return doubleValue;
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeStringData : NodeData
{
    public NodeStringData(string value = "")
    {
        Value = value;
    }

    [YamlConstructor]
    private NodeStringData() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.String;

    [ObservableProperty]
    [YamlMember("watermark")]
    [Key(3)]
    public partial string? Watermark { get; set; }

    [ObservableProperty]
    [YamlMember("multiline")]
    [Key(4)]
    public partial bool AcceptsReturn { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PasswordChar))]
    [YamlMember("secret")]
    [Key(5)]
    public partial bool IsSecret { get; set; }

    [YamlIgnore]
    [IgnoreMember]
    public char PasswordChar => IsSecret ? '*' : (char)0;

    public override object ConvertValue(object? value)
    {
        return value switch
        {
            string str => str,
            byte[] byteArray => Convert.ToBase64String(byteArray),
            Memory<byte> memory => Convert.ToBase64String(memory.ToArray()),
            _ => value?.ToString() ?? string.Empty
        };
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeDateTimeData : NodeData
{
    public NodeDateTimeData(DateTime value)
    {
        Value = value;
    }

    [YamlConstructor]
    private NodeDateTimeData() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.DateTime;

    [ObservableProperty]
    [YamlMember("min")]
    [Key(3)]
    public partial DateTime Min { get; set; } = DateTime.MinValue;

    [ObservableProperty]
    [YamlMember("max")]
    [Key(4)]
    public partial DateTime Max { get; set; } = DateTime.MaxValue;

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
[MessagePackObject(AllowPrivate = true)]
public partial class NodeEnumData : NodeData
{
    [YamlConstructor]
    private NodeEnumData() { }

    [SetsRequiredMembers]
    public NodeEnumData(IEnumerable<string> values)
    {
        Values = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
    }

    public static NodeEnumData FromEnum<T>() where T : struct, Enum
    {
        return new NodeEnumData(Enum.GetValues<T>().Select(t => t.ToFriendlyString()));
    }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Enum;

    [YamlMember("values")]
    [Key(3)]
    public required ISet<string> Values
    {
        get;
        set
        {
            if (value.Count == 0) throw new ArgumentException("Enum values cannot be empty.", nameof(Values));
            if (Value?.ToString() is not { } previousValue || !value.Contains(previousValue)) Value = value.First();
            field = value;
        }
    }

    public override object ConvertValue(object? value)
    {
        var enumValue = value?.ToString();
        if (enumValue == null) throw new FormatException("Value is not an enum, nor convertible to an enum.");
        if (!Values.Contains(enumValue)) throw new FormatException($"Value is not a valid enum value. Valid values are: {string.Join(", ", Values)}.");
        return enumValue;
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeEnumerableData : NodeData
{
    public NodeEnumerableData(IEnumerable items)
    {
        Value = items;
    }

    [YamlConstructor]
    private NodeEnumerableData() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Enumerable;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedItem))]
    [YamlMember("selected_index")]
    [Key(3)]
    public partial int SelectedIndex { get; set; }

    [YamlIgnore]
    [IgnoreMember]
    public object? SelectedItem
    {
        get
        {
            if (SelectedIndex < 0 || Value is not IEnumerable enumerable) return null;
            if (Value is IList list && SelectedIndex < list.Count) return list[SelectedIndex];
            return enumerable.Cast<object?>().Skip(SelectedIndex).FirstOrDefault();
        }
    }

    public override object ConvertValue(object? value)
    {
        if (value is not IEnumerable enumerable) throw new FormatException("Value is not a sequence, nor convertible to a sequence.");
        return enumerable;
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeDictionaryData : NodeData
{
    public NodeDictionaryData(IDictionary dictionary)
    {
        Value = dictionary;
    }

    [YamlConstructor]
    private NodeDictionaryData() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Dictionary;

    public override object ConvertValue(object? value)
    {
        if (value is not IDictionary dictionary) throw new FormatException("Value is not a dictionary, nor convertible to a dictionary.");
        return dictionary;
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeStreamData : NodeData
{
    public NodeStreamData(byte[] value)
    {
        Value = new MemoryStream(value);
    }

    [YamlConstructor]
    private NodeStreamData() { }

    [YamlIgnore]
    [IgnoreMember]
    public override NodeDataType Type => NodeDataType.Stream;

    public override object ConvertValue(object? value)
    {
        return value switch
        {
            byte[] byteArray => new MemoryStream(byteArray),
            Memory<byte> memory => new MemoryStream(memory.ToArray()),
            string str => new MemoryStream(Encoding.UTF8.GetBytes(str)),
            Stream stream => stream,
            _ => throw new FormatException("Value is not a byte array, nor convertible to a byte array.")
        };
    }
}