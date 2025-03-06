using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Models.Workflow;

#pragma warning disable CS0657
[YamlObject]
public partial class WorkflowContext : ObservableObject
{
    [YamlIgnore]
    public WorkflowStartNode StartNode { get; }

    [YamlMember("nodes")]
    public IReadOnlySet<WorkflowNode> Nodes => nodes;

    [YamlMember("connections")]
    public IReadOnlySet<WorkflowNodePortConnection> Connections => connections;

    [ObservableProperty]
    [YamlIgnore]
    public partial WorkflowNodeStates State { get; private set; }

    private readonly HashSet<WorkflowNode> nodes = [];
    private readonly Dictionary<int, WorkflowNode> nodesMap = [];
    private readonly HashSet<WorkflowNodePortConnection> connections = [];

    public WorkflowContext()
    {
        StartNode = new WorkflowStartNode { Owner = this };
        nodes.Add(StartNode);
        nodesMap.Add(StartNode.Id, StartNode);
    }

    [YamlConstructor]
    private WorkflowContext(IReadOnlySet<WorkflowNode> nodes, IReadOnlySet<WorkflowNodePortConnection> connections)
    {
        this.nodes.UnionWith(nodes);
        foreach (var node in nodes)
        {
            node.Owner = this;
            node.PropertyChanged += HandleNodeOnPropertyChanged;
        }
        StartNode = this.nodes.OfType<WorkflowStartNode>().Single();
        nodesMap = this.nodes.ToDictionary(n => n.Id);
        foreach (var connection in connections) AddConnection(connection);
    }

    [YamlIgnore]
    public bool CanStart => State != WorkflowNodeStates.Running;

    [RelayCommand(CanExecute = nameof(CanStart))]
    [property: YamlIgnore]
    private void Start()
    {
        foreach (var node in nodes) node.Reset();
        StartNode.Start();
    }

    [YamlIgnore]
    public bool CanStop => State == WorkflowNodeStates.Running;

    [RelayCommand(CanExecute = nameof(CanStop))]
    [property: YamlIgnore]
    private void Stop()
    {
        foreach (var node in nodes) node.Stop();
    }

    public void AddNode(WorkflowNode node)
    {
        if (node is WorkflowStartNode) throw new InvalidOperationException("A workflow can only have one start node");
        if (!nodesMap.TryAdd(node.Id, node)) throw new InvalidOperationException("Node already exists");
        nodes.Add(node);
        node.PropertyChanged += HandleNodeOnPropertyChanged;
    }

    public void RemoveNode(WorkflowNode node)
    {
        if (!nodes.Remove(node)) return;
        nodesMap.Remove(node.Id);
        foreach (var connection in connections.Where(c => c.InputNodeId == node.Id || c.OutputNodeId == node.Id).ToArray())
            connections.Remove(connection);
        node.PropertyChanged -= HandleNodeOnPropertyChanged;
    }

    private void HandleNodeOnPropertyChanged(object? o, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        State = CalculateState();
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStop));
        StopCommand.NotifyCanExecuteChanged();

        WorkflowNodeStates CalculateState()
        {
            var states = nodes.Aggregate(WorkflowNodeStates.NotStarted, (current, node) => current | node.State);
            if (states.HasFlag(WorkflowNodeStates.Running)) return WorkflowNodeStates.Running;
            if (states.HasFlag(WorkflowNodeStates.Failed)) return WorkflowNodeStates.Failed;
            if (states.HasFlag(WorkflowNodeStates.Completed)) return WorkflowNodeStates.Completed;
            return WorkflowNodeStates.NotStarted;
        }
    }

    /// <summary>
    /// Adds a connection between two nodes, returns the previous connection if any.
    /// </summary>
    /// <param name="connection"></param>
    /// <returns>previous connection if any, need to remove from View</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public WorkflowNodePortConnection? AddConnection(WorkflowNodePortConnection connection)
    {
        if (!nodesMap.TryGetValue(connection.OutputNodeId, out var outputNode))
            throw new InvalidOperationException("Invalid connection: outputNode not found");
        if (!nodesMap.TryGetValue(connection.InputNodeId, out var inputNode))
            throw new InvalidOperationException("Invalid connection: inputNode not found");

        var outputPin = outputNode.GetOutputPin(connection.OutputPinId);
        if (outputPin is null) throw new InvalidOperationException("Invalid connection: OutputPin is null");

        var inputPin = inputNode.GetInputPin(connection.InputPinId);
        WorkflowNodePin? previousConnectedInputPin;
        switch (inputPin)
        {
            case WorkflowNodeControlInputPin controlInputPin when outputPin is WorkflowNodeControlOutputPin controlOutputPin:
            {
                previousConnectedInputPin = controlInputPin.Connection;
                controlInputPin.Connection = controlOutputPin;
                break;
            }
            case WorkflowNodeDataInputPin dataInputPin when outputPin is WorkflowNodeDataOutputPin dataOutputPin:
            {
                previousConnectedInputPin = dataInputPin.Connection;
                dataInputPin.Connection = dataOutputPin;
                break;
            }
            default:
            {
                throw new InvalidOperationException("Invalid connection");
            }
        }

        connections.Add(connection);

        if (previousConnectedInputPin != null)
        {
            var previousConnection = new WorkflowNodePortConnection(
                previousConnectedInputPin.Owner!.Id,
                previousConnectedInputPin.Id,
                inputNode.Id,
                inputPin.Id);
            connections.Remove(previousConnection);
            ConnectionRemoved?.Invoke(previousConnection);
        }

        ConnectionAdded?.Invoke(connection);
    }

    #region Serialization

    /// <summary>
    /// IReadOnlySet is not supported by default (because it doesn't exist in netstandard2.1).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private class InterfaceReadonlySetFormatter<T> : CollectionFormatterBase<T, HashSet<T>, IReadOnlySet<T>>
    {
        protected override HashSet<T> Create(YamlSerializerOptions options) => [];
        protected override void Add(HashSet<T> collection, T value, YamlSerializerOptions options) => collection.Add(value);
        protected override IReadOnlySet<T> Complete(HashSet<T> intermediateCollection) => intermediateCollection;
    }

    static WorkflowContext()
    {
        BuiltinResolver.KnownGenericTypes.Add(typeof(IReadOnlySet<>), typeof(InterfaceReadonlySetFormatter<>));
    }

    public ReadOnlyMemory<byte> SerializeToYaml() =>
        YamlSerializer.Serialize(this, App.Resolve<YamlSerializerOptions>());

    public static WorkflowContext DeserializeFromYaml(ReadOnlyMemory<byte> yaml) =>
        YamlSerializer.Deserialize<WorkflowContext>(yaml, App.Resolve<YamlSerializerOptions>());

    #endregion

}

#pragma warning restore CS0657