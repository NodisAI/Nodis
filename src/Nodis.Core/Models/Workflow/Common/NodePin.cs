using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePack;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

public abstract partial class NodePin(string name) : NodeMember(name)
{
    [YamlIgnore]
    [IgnoreMember]
    public abstract bool IsConnected { get; }

    [ObservableProperty]
    [YamlMember("visible")]
    [Key(4)]
    public partial bool IsVisible { get; set; } = true;
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
[method: YamlConstructor]
public partial class NodeControlOutputPin(string name = "") : NodePin(name)
{
    [YamlIgnore]
    [IgnoreMember]
    public override bool IsConnected => connections.Count > 0;

    [ObservableProperty]
    [YamlIgnore]
    [Key(5)]
    public partial bool? CanExecute { get; set; }

    [IgnoreMember]
    private readonly HashSet<NodeControlInputPin> connections = [];

    /// <summary>
    /// MessagePack constructor for deserialization.
    /// </summary>
    public NodeControlOutputPin() : this(string.Empty) { }

    public bool AddConnection(NodeControlInputPin pin)
    {
        if (!connections.Add(pin)) return false;
        OnPropertyChanged(nameof(IsConnected));
        return true;
    }

    public void RemoveConnection(NodeControlInputPin pin)
    {
        connections.Remove(pin);
        OnPropertyChanged(nameof(IsConnected));
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
[method: YamlConstructor]
public partial class NodeControlInputPin(string name = "") : NodePin(name)
{
    [YamlIgnore]
    [IgnoreMember]
    public override bool IsConnected => Connection != null;

    [YamlIgnore]
    [IgnoreMember]
    public bool ShouldExecute => Connection?.CanExecute is true;

    [YamlIgnore]
    [IgnoreMember]
    public NodeControlOutputPin? Connection
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            if (field != null)
            {
                field.RemoveConnection(this);
                field.PropertyChanged -= HandleConnectionPropertyChanged;
            }
            field = value;
            if (field != null && field.AddConnection(this))
            {
                field.PropertyChanged += HandleConnectionPropertyChanged;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    /// <summary>
    /// MessagePack constructor for deserialization.
    /// </summary>
    private NodeControlInputPin() : this(string.Empty) { }

    protected virtual void HandleConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // sender has only one property, so we don't need to check e.PropertyName
        OnPropertyChanged(nameof(ShouldExecute));
    }
}

public abstract class NodeDataPin(string name, NodeData data) : NodePin(name)
{
    [YamlMember("data")]
    [Key(5)]
    public NodeData Data { get; } = data;
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
[method: YamlConstructor]
public partial class NodeDataOutputPin(string name, NodeData data) : NodeDataPin(name, data)
{
    [YamlIgnore]
    [IgnoreMember]
    public override bool IsConnected => connections.Count > 0;

    /// <summary>
    /// Equivalent to Data.Value
    /// </summary>
    [YamlIgnore]
    [IgnoreMember]
    public object? Value
    {
        get => Data.Value;
        set => Data.Value = value;
    }

    [IgnoreMember]
    private readonly HashSet<NodeDataInputPin> connections = [];

    /// <summary>
    /// MessagePack constructor for deserialization.
    /// </summary>
    private NodeDataOutputPin() : this(string.Empty, NodeAnyData.Shared) { }

    public bool AddConnection(NodeDataInputPin pin)
    {
        if (!connections.Add(pin)) return false;
        OnPropertyChanged(nameof(IsConnected));
        return true;
    }

    public void RemoveConnection(NodeDataInputPin pin)
    {
        connections.Remove(pin);
        OnPropertyChanged(nameof(IsConnected));
    }
}

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class NodeDataInputPin : NodeDataPin
{
    [YamlIgnore]
    [IgnoreMember]
    public override bool IsConnected => Connection != null;

    [YamlIgnore]
    [IgnoreMember]
    public NodeDataOutputPin? Connection
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            if (field != null)
            {
                field.RemoveConnection(this);
                field.Data.PropertyChanged -= HandleDataPropertyChanged;
            }
            field = value;
            if (field != null && field.AddConnection(this))
            {
                field.Data.PropertyChanged += HandleDataPropertyChanged;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    [ObservableProperty]
    [YamlMember("can_input")]
    [Key(6)]
    public partial bool CanUserInput { get; set; } = true;

    [YamlIgnore]
    [IgnoreMember]
    public NodePinCondition? Condition { get; init; }

    [YamlIgnore]
    [IgnoreMember]
    public object? Value
    {
        get
        {
            try
            {
                return Connection != null ? Data.ConvertValue(Connection.Data.Value) : Data.Value;
            }
            catch (Exception e)
            {
                throw new NodePinException(this, e);
            }
        }
    }

    [YamlConstructor]
    public NodeDataInputPin(string name, NodeData data) : base(name, data)
    {
        Data.PropertyChanged += HandleDataPropertyChanged;
    }

    /// <summary>
    /// MessagePack constructor for deserialization.
    /// </summary>
    private NodeDataInputPin() : this(string.Empty, NodeAnyData.Shared) { }

    protected virtual void HandleDataPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(Value)); // notify Value property change
}

public class NodePinException(NodePin pin, Exception innerException) : Exception(pin.Name, innerException);

public abstract record NodePinCondition;

public record NodePinValueCondition(string PinName, Predicate<NodeData> Predicate) : NodePinCondition;