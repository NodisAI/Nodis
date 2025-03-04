using System.ComponentModel;
using System.Data;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.ColorTheme;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

public abstract class WorkflowNodePin(string name) : WorkflowNodeMember(name)
{
    [YamlIgnore]
    public abstract Color Color { get; }

    [YamlIgnore]
    public abstract bool IsConnected { get; }

    [YamlIgnore]
    public abstract double StrokeThickness { get; }
}

[YamlObject]
public partial class WorkflowNodeControlOutputPin(string name = "") : WorkflowNodePin(name)
{
    [YamlIgnore]
    public override Color Color { get; } = Color.FromRgb(255, 255, 255);

    [YamlIgnore]
    public override bool IsConnected => connections.Count > 0;

    [YamlIgnore]
    public override double StrokeThickness => 5d;

    [ObservableProperty]
    [YamlIgnore]
    public partial bool? CanExecute { get; set; }

    private readonly HashSet<WorkflowNodeControlInputPin> connections = [];

    public bool AddConnection(WorkflowNodeControlInputPin pin)
    {
        if (!connections.Add(pin)) return false;
        OnPropertyChanged(nameof(IsConnected));
        return true;
    }

    public void RemoveConnection(WorkflowNodeControlInputPin pin)
    {
        connections.Remove(pin);
        OnPropertyChanged(nameof(IsConnected));
    }
}

[YamlObject]
public partial class WorkflowNodeControlInputPin(string name = "") : WorkflowNodePin(name)
{
    [YamlIgnore]
    public override Color Color { get; } = Color.FromRgb(255, 255, 255);

    [YamlIgnore]
    public override bool IsConnected => Connection != null;

    [YamlIgnore]
    public override double StrokeThickness => 5d;

    [YamlIgnore]
    public bool ShouldExecute => Connection?.CanExecute is true;

    [YamlIgnore]
    public WorkflowNodeControlOutputPin? Connection
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

    protected virtual void HandleConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // sender has only one property, so we don't need to check e.PropertyName
        OnPropertyChanged(nameof(ShouldExecute));
    }
}

public abstract class WorkflowNodeDataPin(string name, WorkflowNodeData data) : WorkflowNodePin(name)
{
    [YamlMember("data")]
    public WorkflowNodeData Data { get; } = data;
}

[YamlObject]
public partial class WorkflowNodeDataOutputPin : WorkflowNodeDataPin
{
    [YamlIgnore]
    public override Color Color => OpenColors.Column6[(int)Data.Type % OpenColors.Column6.Length];

    [YamlIgnore]
    public override bool IsConnected => connections.Count > 0;

    [YamlIgnore]
    public override double StrokeThickness => 3d;

    private readonly HashSet<WorkflowNodeDataInputPin> connections = [];

    public WorkflowNodeDataOutputPin(string name, WorkflowNodeData data) : base(name, data)
    {
        Data.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkflowNodeData.Type)) OnPropertyChanged(nameof(Color));
        };
    }

    public bool AddConnection(WorkflowNodeDataInputPin pin)
    {
        if (!connections.Add(pin)) return false;
        OnPropertyChanged(nameof(IsConnected));
        return true;
    }

    public void RemoveConnection(WorkflowNodeDataInputPin pin)
    {
        connections.Remove(pin);
        OnPropertyChanged(nameof(IsConnected));
    }
}

[YamlObject]
public partial class WorkflowNodeDataInputPin : WorkflowNodeDataPin
{
    [YamlIgnore]
    public override Color Color => OpenColors.Column6[(int)Data.Type % OpenColors.Column6.Length];

    [YamlIgnore]
    public override bool IsConnected => Connection != null;

    [YamlIgnore]
    public override double StrokeThickness => 3d;

    public WorkflowNodeDataOutputPin? Connection
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            if (field != null)
            {
                field.RemoveConnection(this);
                field.Data.PropertyChanged -= HandleConnectionDataPropertyChanged;
            }
            field = value;
            if (field != null && field.AddConnection(this))
            {
                field.Data.PropertyChanged += HandleConnectionDataPropertyChanged;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    [YamlMember("has_data")]
    public bool HasData { get; init; }

    [YamlIgnore]
    public object? Value
    {
        get
        {
            try
            {
                if (Connection != null) return Data.ConvertValue(Connection.Data.Value);
                if (HasData) return Data.Value;
            }
            catch (Exception e)
            {
                throw new WorkflowNodePortException(this, e);
            }

            throw new WorkflowNodePortException(this, new DataException("No data available."));
        }
    }

    [YamlConstructor]
    public WorkflowNodeDataInputPin(string name, WorkflowNodeData data) : base(name, data)
    {
        Data.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(WorkflowNodeData.Type):
                    OnPropertyChanged(nameof(Color));
                    break;
                case nameof(WorkflowNodeData.Value):
                    OnPropertyChanged(nameof(Value));
                    break;
            }
        };
    }

    public WorkflowNodeDataInputPin(string name, WorkflowNodeData data, bool hasData) : this(name, data)
    {
        HasData = hasData;
    }

    protected virtual void HandleConnectionDataPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(Value));  // notify Value property change
}

public class WorkflowNodePortException(WorkflowNodePin pin, Exception innerException) : Exception(pin.Name, innerException);