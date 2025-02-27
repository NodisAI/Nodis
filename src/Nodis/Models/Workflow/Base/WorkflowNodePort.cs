using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nodis.Models.Workflow;

public abstract partial class WorkflowNodePort(string name, Type dataType) : WorkflowNodeProperty(name)
{
    public Type DataType => dataType;

    public Color Color
    {
        get
        {
            var hashCode = dataType.GetHashCode();
            var r = (byte)((hashCode & 0xFF0000) >> 16);
            var g = (byte)((hashCode & 0x00FF00) >> 8);
            var b = (byte)(hashCode & 0x0000FF);
            return Color.FromRgb(r, g, b);
        }
    }

    /// <summary>
    /// We use object here so that type conversion can be done between two ports.
    /// </summary>
    [ObservableProperty]
    public partial object? Value { get; set; }

    [ObservableProperty]
    public partial bool HasValue { get; set; }
}

public class WorkflowNodeOutputPort(string name, Type dataType) : WorkflowNodePort(name, dataType)
{
    public WorkflowNodeInputPort? Connection
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            if (value != null) value.Connection = null;
            field = value;
            if (value != null && !Equals(value.Connection, this)) value.Connection = this;
            OnPropertyChanged();
        }
    }
}

public class WorkflowNodeInputPort(string name, Type dataType) : WorkflowNodePort(name, dataType)
{
    public WorkflowNodeOutputPort? Connection
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            if (value != null) value.Connection = null;
            field = value;
            if (value != null && !Equals(value.Connection, this)) value.Connection = this;
            OnPropertyChanged();
        }
    }
}
