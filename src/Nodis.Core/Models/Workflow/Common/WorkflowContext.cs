﻿using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessagePack;
using Nodis.Core.Extensions;
using Nodis.Core.Networking;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Core.Models.Workflow;

#pragma warning disable CS0657
[YamlObject]
// [MessagePackObject(AllowPrivate = true)]
public partial class WorkflowContext : ObservableObject
{
    public delegate void NodeChangedEventHandler(Node node);
    public delegate void ConnectionChangedEventHandler(NodePortConnection connection);

    [IgnoreMember]
    private readonly NetworkObjectTracker tracker;

    [YamlIgnore]
    [Key(0)]
    public Guid NetworkObjectId
    {
        get => tracker.Id;
        internal set => tracker.Id = value;
    }

    [YamlIgnore]
    [IgnoreMember]
    public StartNode StartNode { get; }

    [YamlMember("start_node_x")]
    [IgnoreMember]
    public double StartNodeX
    {
        get => StartNode.X;
        set => StartNode.X = value;
    }

    [YamlMember("start_node_y")]
    [IgnoreMember]
    public double StartNodeY
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
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [YamlIgnore]
    public partial NodeStates State { get; internal set; }

    public event NodeChangedEventHandler? NodeAdded;
    public event NodeChangedEventHandler? NodeRemoved;
    public event ConnectionChangedEventHandler? ConnectionAdded;
    public event ConnectionChangedEventHandler? ConnectionRemoved;

    [IgnoreMember] private readonly HashSet<Node> nodes = [];
    [IgnoreMember] private readonly Dictionary<ulong, Node> nodesMap = [];
    [IgnoreMember] private readonly HashSet<NodePortConnection> connections = [];

    public WorkflowContext()
    {
        tracker = new NetworkObjectTracker(this);
        StartNode = new StartNode { Owner = this };
        nodes.Add(StartNode);
        nodesMap.Add(StartNode.Id, StartNode);
    }

    [YamlConstructor]
    private WorkflowContext(
        IList<BuiltInNode> builtInNodes,
        // IList<UserNode> userNodes,
        IReadOnlySet<NodePortConnection> connections) : this()
    {
        nodes.UnionWith(builtInNodes);
        nodesMap.AddRange(builtInNodes.Select(n => new KeyValuePair<ulong, Node>(n.Id, n)));
        // nodes.UnionWith(userNodes);
        foreach (var node in nodes)
        {
            node.Owner = this;
            node.PropertyChanged += HandleNodeOnPropertyChanged;
        }
        foreach (var connection in connections) AddConnection(connection);
    }

    [YamlIgnore]
    [IgnoreMember]
    public bool CanStart => State != NodeStates.Running;

    [RelayCommand(CanExecute = nameof(CanStart))]
    [property: YamlIgnore]
    [property: IgnoreMember]
    private void Start()
    {
        foreach (var node in nodes) node.Reset();
        StartNode.Start();
    }

    [YamlIgnore]
    [IgnoreMember]
    public bool CanStop => State == NodeStates.Running;

    [RelayCommand(CanExecute = nameof(CanStop))]
    [property: YamlIgnore]
    [property: IgnoreMember]
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

    public void AddConnection(ulong outputNodeId, ulong outputPinId, ulong inputNodeId, ulong inputPinId) =>
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