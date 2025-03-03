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

    [YamlIgnore]
    public WorkflowNodeStates State
    {
        get
        {
            var state = nodes.Aggregate(WorkflowNodeStates.NotStarted, (current, node) => current | node.State);
            if (state.HasFlag(WorkflowNodeStates.Running)) return WorkflowNodeStates.Running;
            if (state.HasFlag(WorkflowNodeStates.Failed)) return WorkflowNodeStates.Failed;
            if (state.HasFlag(WorkflowNodeStates.Completed)) return WorkflowNodeStates.Completed;
            return WorkflowNodeStates.NotStarted;
        }
    }

    private readonly HashSet<WorkflowNode> nodes = [];
    private readonly Dictionary<int, WorkflowNode> nodesMap = [];
    private readonly HashSet<WorkflowNodePortConnection> connections = [];

    public WorkflowContext()
    {
        StartNode = new WorkflowStartNode();
        nodes.Add(StartNode);
        nodesMap.Add(StartNode.Id, StartNode);
    }

    [YamlConstructor]
    private WorkflowContext(IReadOnlySet<WorkflowNode> nodes, IReadOnlySet<WorkflowNodePortConnection> connections)
    {
        this.nodes.UnionWith(nodes);
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
        Stop();
        StartNode.Start();
    }

    [YamlIgnore]
    public bool CanStop => State == WorkflowNodeStates.Running;

    [RelayCommand(CanExecute = nameof(CanStop))]
    [property: YamlIgnore]
    private void Stop()
    {
        StartNode.Stop();
        foreach (var node in nodes) node.CancelExecution();
    }

    public void AddNode(WorkflowNode node)
    {
        if (node is WorkflowStartNode) throw new InvalidOperationException("A workflow can only have one start node");
        if (!nodesMap.TryAdd(node.Id, node)) throw new InvalidOperationException("Node already exists");
        nodes.Add(node);
        node.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
        };
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

        var outputPort = outputNode.GetOutputPort(connection.OutputPortId);
        if (outputPort is null) throw new InvalidOperationException("Invalid connection: outputPort is null");

        var inputPort = inputNode.GetInputPort(connection.InputPortId);
        WorkflowNodePort? previousConnectedInputPort;
        switch (inputPort)
        {
            case WorkflowNodeControlInputPort controlInputPort when outputPort is WorkflowNodeControlOutputPort controlOutputPort:
            {
                previousConnectedInputPort = controlInputPort.Connection;
                controlInputPort.Connection = controlOutputPort;
                break;
            }
            case WorkflowNodeDataInputPort dataInputPort when outputPort is WorkflowNodeDataOutputPort dataOutputPort:
            {
                previousConnectedInputPort = dataInputPort.Connection;
                dataInputPort.Connection = dataOutputPort;
                break;
            }
            default:
            {
                throw new InvalidOperationException("Invalid connection");
            }
        }

        connections.Add(connection);

        if (previousConnectedInputPort != null)
        {
            var previousConnection = new WorkflowNodePortConnection(
                previousConnectedInputPort.Owner!.Id,
                previousConnectedInputPort.Id,
                inputNode.Id,
                inputPort.Id);
            connections.Remove(previousConnection);
            return previousConnection;
        }

        return null;
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

    private static YamlSerializerOptions YamlSerializerOptions => new()
    {
        Resolver = CompositeResolver.Create(
            [
                new WorkflowNodePortConnectionYamlFormatter()
            ],
            [
                StandardResolver.Instance
            ]
        )
    };

    public ReadOnlyMemory<byte> SerializeToYaml() =>
        YamlSerializer.Serialize(this, YamlSerializerOptions);

    public static WorkflowContext DeserializeFromYaml(ReadOnlyMemory<byte> yaml) =>
        YamlSerializer.Deserialize<WorkflowContext>(yaml, YamlSerializerOptions);

    #endregion

}

#pragma warning restore CS0657