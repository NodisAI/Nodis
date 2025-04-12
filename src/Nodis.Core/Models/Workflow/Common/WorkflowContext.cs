using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Core.Models.Workflow;

#pragma warning disable CS0657
[YamlObject]
public partial class WorkflowContext : ObservableObject
{
    public delegate void NodeChangedEventHandler(Node node);
    public delegate void ConnectionChangedEventHandler(NodePortConnection connection);

    [YamlIgnore]
    public StartNode StartNode { get; }

    [YamlMember("start_node_x")]
    public double X
    {
        get => StartNode.X;
        set => StartNode.X = value;
    }

    [YamlMember("start_node_y")]
    public double Y
    {
        get => StartNode.Y;
        set => StartNode.Y = value;
    }

    [YamlIgnore]
    public IReadOnlySet<Node> Nodes => nodes;

    [YamlMember("built_in_nodes")]
    private IList<BuiltInNode> BuiltInNodes => nodes.OfType<BuiltInNode>().ToList();

    // [YamlMember("user_nodes")]
    // private IList<UserNode> UserNodes => nodes.OfType<UserNode>().ToList();

    [YamlMember("connections")]
    public IReadOnlySet<NodePortConnection> Connections => connections;

    [ObservableProperty]
    [YamlIgnore]
    public partial NodeStates State { get; private set; }

    public event NodeChangedEventHandler? NodeAdded;
    public event NodeChangedEventHandler? NodeRemoved;
    public event ConnectionChangedEventHandler? ConnectionAdded;
    public event ConnectionChangedEventHandler? ConnectionRemoved;

    private readonly HashSet<Node> nodes = [];
    private readonly Dictionary<int, Node> nodesMap = [];
    private readonly HashSet<NodePortConnection> connections = [];

    public WorkflowContext()
    {
        StartNode = new StartNode { Owner = this };
        nodes.Add(StartNode);
        nodesMap.Add(StartNode.Id, StartNode);
    }

    [YamlConstructor]
    private WorkflowContext(
        IList<BuiltInNode> builtInNodes,
        // IList<UserNode> userNodes,
        IReadOnlySet<NodePortConnection> connections)
    {
        nodes.UnionWith(builtInNodes);
        // nodes.UnionWith(userNodes);
        foreach (var node in nodes)
        {
            node.Owner = this;
            node.PropertyChanged += HandleNodeOnPropertyChanged;
        }
        StartNode = nodes.OfType<StartNode>().Single();
        nodesMap = nodes.ToDictionary(n => n.Id);
        foreach (var connection in connections) AddConnection(connection);
    }

    [YamlIgnore]
    public bool CanStart => State != NodeStates.Running;

    [RelayCommand(CanExecute = nameof(CanStart))]
    [property: YamlIgnore]
    private void Start()
    {
        foreach (var node in nodes) node.Reset();
        StartNode.Start();
    }

    [YamlIgnore]
    public bool CanStop => State == NodeStates.Running;

    [RelayCommand(CanExecute = nameof(CanStop))]
    [property: YamlIgnore]
    private void Stop()
    {
        foreach (var node in nodes) node.Stop();
    }

    public void AddNode(Node node)
    {
        if (node is StartNode) throw new InvalidOperationException("A workflow can only have one start node");
        if (!nodesMap.TryAdd(node.Id, node)) throw new InvalidOperationException("Node already exists");
        nodes.Add(node);
        node.PropertyChanged += HandleNodeOnPropertyChanged;
        NodeAdded?.Invoke(node);
    }

    public void RemoveNode(Node node)
    {
        if (!nodes.Remove(node)) return;
        nodesMap.Remove(node.Id);
        foreach (var connection in connections.Where(c => c.InputNodeId == node.Id || c.OutputNodeId == node.Id).ToArray())
            connections.Remove(connection);
        node.PropertyChanged -= HandleNodeOnPropertyChanged;
        NodeRemoved?.Invoke(node);
    }

    private void HandleNodeOnPropertyChanged(object? o, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        State = CalculateState();
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStop));
        StopCommand.NotifyCanExecuteChanged();

        NodeStates CalculateState()
        {
            var states = nodes.Aggregate(NodeStates.NotStarted, (current, node) => current | node.State);
            if (states.HasFlag(NodeStates.Running)) return NodeStates.Running;
            if (states.HasFlag(NodeStates.Failed)) return NodeStates.Failed;
            if (states.HasFlag(NodeStates.Completed)) return NodeStates.Completed;
            return NodeStates.NotStarted;
        }
    }

    public void AddConnection(int outputNodeId, int outputPinId, int inputNodeId, int inputPinId) =>
        AddConnection(new NodePortConnection(outputNodeId, outputPinId, inputNodeId, inputPinId));

    public void AddConnection(NodePortConnection connection)
    {
        if (!nodesMap.TryGetValue(connection.OutputNodeId, out var outputNode))
            throw new InvalidOperationException("Invalid connection: outputNode not found");
        if (!nodesMap.TryGetValue(connection.InputNodeId, out var inputNode))
            throw new InvalidOperationException("Invalid connection: inputNode not found");

        var outputPin = outputNode.GetOutputPin(connection.OutputPinId);
        if (outputPin is null) throw new InvalidOperationException("Invalid connection: OutputPin is null");

        var inputPin = inputNode.GetInputPin(connection.InputPinId);
        NodePin? previousConnectedInputPin;
        switch (inputPin)
        {
            case NodeControlInputPin controlInputPin when outputPin is NodeControlOutputPin controlOutputPin:
            {
                previousConnectedInputPin = controlInputPin.Connection;
                controlInputPin.Connection = controlOutputPin;
                break;
            }
            case NodeDataInputPin dataInputPin when outputPin is NodeDataOutputPin dataOutputPin:
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
            var previousConnection = new NodePortConnection(
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
        YamlSerializer.Serialize(this, ServiceLocator.Resolve<YamlSerializerOptions>());

    public static WorkflowContext DeserializeFromYaml(ReadOnlyMemory<byte> yaml) =>
        YamlSerializer.Deserialize<WorkflowContext>(yaml, ServiceLocator.Resolve<YamlSerializerOptions>());

    #endregion
}

#pragma warning restore CS0657