using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePack;
using Nodis.Core.Extensions;
using Nodis.Core.Networking;
using ObservableCollections;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[Flags]
public enum NodeStates
{
    NotStarted = 0x0,
    Running = 0x1,
    Completed = 0x2,
    Failed = 0x4
}

public abstract partial class Node : ObservableObject
{
    private static int globalId;

    [YamlIgnore]
    public WorkflowContext? Owner { get; internal set; }

    public abstract string Name { get; }

    // public abstract string Description { get; }

    [IgnoreMember]
    private readonly NetworkObjectTracker tracker;

    [YamlIgnore]
    [Key(0)]
    public Guid NetworkObjectId
    {
        get => tracker.Id;
        internal set => tracker.Id = value;
    }

    [YamlMember("id")]
    public int Id { get; protected init; }

    /// <summary>
    /// A user editable comment of the node.
    /// </summary>
    [YamlMember("comment")]
    public string? Comment { get; set; }

    [ObservableProperty]
    [YamlMember("x")]
    public partial double X { get; set; }

    [ObservableProperty]
    [YamlMember("y")]
    public partial double Y { get; set; }

    [YamlIgnore]
    public NodeControlInputPin? ControlInput
    {
        get;
        protected set
        {
            if (Equals(value, field)) return;
            if (field != null)
            {
                field.Owner = null;
                field.PropertyChanged -= HandleControlInputPropertyChanged;
            }
            field = value;
            if (field != null)
            {
                field.Owner = this;
                field.PropertyChanged += HandleControlInputPropertyChanged;
            }
        }
    }

    [YamlIgnore]
    public NodePropertyCollection<NodeControlOutputPin> ControlOutputs { get; }

    [YamlIgnore]
    public NodePropertyCollection<NodeDataInputPin> DataInputs { get; }

    [YamlIgnore]
    public bool IsDataInputsDynamic { get; set; }

    [YamlIgnore]
    public NodePropertyCollection<NodeDataOutputPin> DataOutputs { get; }

    [YamlIgnore]
    public NodePropertyCollection<NodeProperty> Properties { get; }

    [YamlIgnore]
    public virtual object? FooterContent => null;

    [ObservableProperty]
    [YamlIgnore]
    public partial NodeStates State { get; protected set; } = NodeStates.NotStarted;

    [ObservableProperty]
    [YamlIgnore]
    [IgnoreMember]
    public partial string? ErrorMessage { get; protected set; }

    protected Node(int id)
    {
        tracker = new NetworkObjectTracker(this);
        Id = id;
        ControlOutputs = new NodePropertyCollection<NodeControlOutputPin>(this);
        DataInputs = new NodePropertyCollection<NodeDataInputPin>(this);
        DataOutputs = new NodePropertyCollection<NodeDataOutputPin>(this);
        Properties = new NodePropertyCollection<NodeProperty>(this);

        DataInputs.CollectionChanged += (in NotifyCollectionChangedEventArgs<NodeDataInputPin> args) =>
        {
            if (args.Action != NotifyCollectionChangedAction.Add) return;
            if (args.IsSingleItem) ProcessNodePinCondition(args.NewItem);
            else foreach (var newItem in args.NewItems) ProcessNodePinCondition(newItem);
        };

        void ProcessNodePinCondition(NodeDataInputPin pin)
        {
            switch (pin.Condition)
            {
                case NodePinValueCondition valueCondition:
                {
                    var target = DataInputs[valueCondition.PinName];
                    target.Data.PropertyChanged += (_, _) => pin.IsVisible = valueCondition.Predicate(target.Data);
                    pin.IsVisible = valueCondition.Predicate(target.Data);
                    break;
                }
            }
        }
    }

    protected Node() : this(Interlocked.Increment(ref globalId)) { }

    public override int GetHashCode() => Id;
    public override bool Equals(object? obj) => obj is Node other && Id == other.Id;

    internal int GetAvailableMemberId()
    {
        var id = 0;
        while (true)
        {
            id++;
            if (ControlInput?.Id == id) continue;
            if (ControlOutputs.Any(p => p.Id == id)) continue;
            if (DataInputs.Any(p => p.Id == id)) continue;
            if (DataOutputs.Any(p => p.Id == id)) continue;
            return id;
        }
    }

    public NodePin? GetInputPin(int id) =>
        ControlInput?.Id == id ? ControlInput : DataInputs.FirstOrDefault(p => p.Id == id);

    public NodePin? GetOutputPin(int id) =>
        ControlOutputs.FirstOrDefault<NodePin>(p => p.Id == id) ?? DataOutputs.FirstOrDefault(p => p.Id == id);

    #region Execution

    private CancellationTokenSource? cancellationTokenSource;
    private readonly object executeLock = new();

    internal void Reset()
    {
        lock (executeLock)
        {
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            foreach (var controlOutput in ControlOutputs) controlOutput.CanExecute = null;
            State = NodeStates.NotStarted;
        }
    }

    internal void Stop()
    {
        lock (executeLock)
        {
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }
    }

    private async void HandleControlInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NodeControlInputPin.ShouldExecute)) return;
        var shouldExecute = sender.To<NodeControlInputPin>()!.ShouldExecute;

        CancellationToken cancellationToken;
        lock (executeLock)
        {
            if (State == NodeStates.Running && shouldExecute ||
                State != NodeStates.Running && !shouldExecute) return;

            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            if (!shouldExecute) return;

            State = NodeStates.Running;
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
        }

        try
        {
            await ExecuteImplAsync(cancellationToken);
            State = NodeStates.Completed;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.GetFriendlyMessage();
            State = NodeStates.Failed;
        }
    }

    protected abstract Task ExecuteImplAsync(CancellationToken cancellationToken);

    #endregion
}