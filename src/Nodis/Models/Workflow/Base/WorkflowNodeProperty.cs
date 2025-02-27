using CommunityToolkit.Mvvm.ComponentModel;

namespace Nodis.Models.Workflow;

public abstract class WorkflowNodeProperty(string name) : ObservableValidator
{
    public string Name => name;

    public WorkflowNode? Owner { get; internal set; }
}

public abstract partial class WorkflowNodeProperty<T>(string name, in T defaultValue) : WorkflowNodeProperty(name)
{
    [ObservableProperty]
    public partial T Value { get; set; } = defaultValue;
}

public class WorkflowNodeStringProperty(string name) : WorkflowNodeProperty<string>(name, string.Empty);

public class WorkflowNodeIntegerProperty(string name) : WorkflowNodeProperty<int>(name, 0);

public class WorkflowNodeDecimalProperty(string name) : WorkflowNodeProperty<double>(name, 0d);

public class WorkflowNodeBooleanProperty(string name) : WorkflowNodeProperty<bool>(name, false);

public partial class WorkflowNodeSelectionProperty(string name, object[] items) : WorkflowNodeProperty<object[]>(name, items)
{
    [ObservableProperty]
    public partial object? SelectedItem { get; set; }
}