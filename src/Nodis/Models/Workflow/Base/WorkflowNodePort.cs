using System.ComponentModel;
using System.Data;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.ColorTheme;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

public abstract class WorkflowNodePort(string name) : WorkflowNodeMember(name)
{
    [YamlIgnore]
    public abstract Color Color { get; }

    [YamlIgnore]
    public abstract double StrokeThickness { get; }
}

public interface IWorkflowNodeOutputPort<TInputPort, TOutputPort>
    where TInputPort : IWorkflowNodeInputPort<TInputPort, TOutputPort>
    where TOutputPort : IWorkflowNodeOutputPort<TInputPort, TOutputPort>
{
    /// <summary>
    /// An output port can have multiple connections.
    /// </summary>
    HashSet<TInputPort> Connections { get; }
}

public interface IWorkflowNodeInputPort<TInputPort, TOutputPort>
    where TInputPort : IWorkflowNodeInputPort<TInputPort, TOutputPort>
    where TOutputPort : IWorkflowNodeOutputPort<TInputPort, TOutputPort>
{
    /// <summary>
    /// An input port can only have one connection.
    /// </summary>
    TOutputPort? Connection { get; set; }
}

[YamlObject]
public partial class WorkflowNodeControlOutputPort(string name = "") :
    WorkflowNodePort(name), IWorkflowNodeOutputPort<WorkflowNodeControlInputPort, WorkflowNodeControlOutputPort>
{
    [YamlIgnore]
    public override Color Color { get; } = Color.FromRgb(255, 255, 255);

    [YamlIgnore]
    public override double StrokeThickness => 5d;

    [YamlIgnore]
    public HashSet<WorkflowNodeControlInputPort> Connections { get; } = [];

    [ObservableProperty]
    [YamlIgnore]
    public partial bool CanExecute { get; set; }
}

[YamlObject]
public partial class WorkflowNodeControlInputPort(string name = "") :
    WorkflowNodePort(name), IWorkflowNodeInputPort<WorkflowNodeControlInputPort, WorkflowNodeControlOutputPort>
{
    [YamlIgnore]
    public override Color Color { get; } = Color.FromRgb(255, 255, 255);

    [YamlIgnore]
    public override double StrokeThickness => 5d;

    [YamlIgnore]
    public bool ShouldExecute => Connection?.CanExecute is true;

    [YamlIgnore]
    public WorkflowNodeControlOutputPort? Connection
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            if (field != null)
            {
                field.Connections.Remove(this);
                field.PropertyChanged -= HandleConnectionPropertyChanged;
            }
            field = value;
            if (field != null && field.Connections.Add(this))
            {
                field.PropertyChanged += HandleConnectionPropertyChanged;
            }
            OnPropertyChanged();
        }
    }

    protected virtual void HandleConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // sender has only one property, so we don't need to check e.PropertyName
        OnPropertyChanged(nameof(ShouldExecute));
    }
}

public abstract class WorkflowNodeDataPort(string name, WorkflowNodeData data) : WorkflowNodePort(name)
{
    [YamlMember("data")]
    public WorkflowNodeData Data { get; } = data;
}

[YamlObject]
public partial class WorkflowNodeDataOutputPort :
    WorkflowNodeDataPort, IWorkflowNodeOutputPort<WorkflowNodeDataInputPort, WorkflowNodeDataOutputPort>
{
    public WorkflowNodeDataOutputPort(string name, WorkflowNodeData data) : base(name, data)
    {
        Data.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkflowNodeData.Type)) OnPropertyChanged(nameof(Color));
        };
    }

    [YamlIgnore]
    public override Color Color => OpenColors.Column6[(int)Data.Type % OpenColors.Column6.Length];

    [YamlIgnore]
    public override double StrokeThickness => 3d;

    [YamlIgnore]
    public HashSet<WorkflowNodeDataInputPort> Connections { get; } = [];
}

[YamlObject]
public partial class WorkflowNodeDataInputPort :
    WorkflowNodeDataPort, IWorkflowNodeInputPort<WorkflowNodeDataInputPort, WorkflowNodeDataOutputPort>
{
    [YamlConstructor]
    public WorkflowNodeDataInputPort(string name, WorkflowNodeData data) : base(name, data)
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

    public WorkflowNodeDataInputPort(string name, WorkflowNodeData data, bool hasData) : this(name, data)
    {
        HasData = hasData;
    }

    [YamlIgnore]
    public override Color Color => OpenColors.Column6[(int)Data.Type % OpenColors.Column6.Length];

    [YamlIgnore]
    public override double StrokeThickness => 3d;

    public WorkflowNodeDataOutputPort? Connection
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            if (field != null)
            {
                field.Connections.Remove(this);
                field.Data.PropertyChanged -= HandleConnectionDataPropertyChanged;
            }
            field = value;
            if (field != null && field.Connections.Add(this))
            {
                field.Data.PropertyChanged += HandleConnectionDataPropertyChanged;
            }
            OnPropertyChanged();
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

    protected virtual void HandleConnectionDataPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(Value));  // notify Value property change
}

public class WorkflowNodePortException(WorkflowNodePort port, Exception innerException) : Exception(port.Name, innerException);