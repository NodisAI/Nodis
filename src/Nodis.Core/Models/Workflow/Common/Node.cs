using System.Collections.Frozen;
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
    private static ulong globalId = 1; // 1 is reserved for the StartNode

    [YamlIgnore]
    [IgnoreMember]
    public WorkflowContext? Owner { get; internal set; }

    [IgnoreMember]
    private readonly NetworkObjectTracker tracker;

    [YamlIgnore]
    [Key(0)]
    public Guid NetworkObjectId
    {
        get => tracker.Id;
        protected set => tracker.Id = value;
    }

    [YamlMember("id")]
    [Key(1)]
    public ulong Id
    {
        get
        {
            if (field == 0) Interlocked.CompareExchange(ref field, Interlocked.Increment(ref globalId), 0);
            return field;
        }
        protected init;
    }

    /// <summary>
    /// A user editable comment of the node.
    /// </summary>
    [YamlMember("comment")]
    [Key(2)]
    public string? Comment { get; set; }

    [ObservableProperty]
    [YamlMember("x")]
    [Key(3)]
    public partial double X { get; set; }

    [ObservableProperty]
    [YamlMember("y")]
    [Key(4)]
    public partial double Y { get; set; }

    [YamlIgnore]
    [IgnoreMember]
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
    [IgnoreMember]
    public NodePropertyList<NodeControlOutputPin> ControlOutputs { get; }

    [Obsolete("Only for serialization")]
    [YamlIgnore]
    [Key(5)]
    protected Guid ControlOutputsNetworkObjectId
    {
        get => ControlOutputs.tracker.Id;
        set => ControlOutputs.tracker.Id = value;
    }

    [YamlIgnore]
    [IgnoreMember]
    public NodePropertyList<NodeDataInputPin> DataInputs { get; }

    [Obsolete("Only for serialization")]
    [YamlIgnore]
    [Key(6)]
    protected Guid DataInputsNetworkObjectId
    {
        get => DataInputs.tracker.Id;
        set => DataInputs.tracker.Id = value;
    }

    [Obsolete("Only for serialization")]
    [YamlMember("input_values")]
    [Key(7)]
    protected IReadOnlyDictionary<string, object?> DataInputValues
    {
        get => DataInputs.Where(p => p is { CanUserInput: true, Data.Type: not NodeDataType.Object and not NodeDataType.Stream })
            .ToFrozenDictionary(p => p.Name, p => p.Data.Value);
        init
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // Deserializer may set this to null
            if (value == null) return;
            foreach (var (name, data) in value) DataInputs[name].Data.Value = data;
        }
    }

    [YamlIgnore]
    [IgnoreMember]
    public bool IsDataInputsDynamic { get; protected init; }

    [YamlIgnore]
    [IgnoreMember]
    public NodePropertyList<NodeDataOutputPin> DataOutputs { get; }

    [Obsolete("Only for serialization")]
    [YamlIgnore]
    [Key(8)]
    protected Guid DataOutputsNetworkObjectId
    {
        get => DataOutputs.tracker.Id;
        set => DataOutputs.tracker.Id = value;
    }

    [YamlIgnore]
    [IgnoreMember]
    public NodePropertyList<NodeProperty> Properties { get; }

    [Obsolete("Only for serialization")]
    [YamlIgnore]
    [Key(9)]
    protected Guid PropertiesNetworkObjectId
    {
        get => Properties.tracker.Id;
        set => Properties.tracker.Id = value;
    }

    [ObservableProperty]
    [YamlIgnore]
    [Key(10)]
    public partial NodeStates State { get; protected set; } = NodeStates.NotStarted;

    [ObservableProperty]
    [YamlIgnore]
    [Key(11)]
    public partial string? ErrorMessage { get; protected set; }

    protected Node()
    {
        tracker = new NetworkObjectTracker(this);
        ControlOutputs = new NodePropertyList<NodeControlOutputPin>(this);
        DataInputs = new NodePropertyList<NodeDataInputPin>(this);
        DataOutputs = new NodePropertyList<NodeDataOutputPin>(this);
        Properties = new NodePropertyList<NodeProperty>(this);

        DataInputs.CollectionChanged += (in NotifyCollectionChangedEventArgs<NodeDataInputPin> args) =>
        {
            if (args.Action != NotifyCollectionChangedAction.Add) return;
            if (args.IsSingleItem) ProcessNodePinCondition(args.NewItem);
            else
                foreach (var newItem in args.NewItems)
                    ProcessNodePinCondition(newItem);
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

    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => obj is Node other && Id == other.Id;

    internal ulong GetAvailableMemberId()
    {
        var id = 0UL;
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

    public NodePin? GetInputPin(ulong id) =>
        ControlInput?.Id == id ? ControlInput : DataInputs.FirstOrDefault(p => p.Id == id);

    public NodePin? GetOutputPin(ulong id) =>
        ControlOutputs.FirstOrDefault<NodePin>(p => p.Id == id) ?? DataOutputs.FirstOrDefault(p => p.Id == id);

    #region Execution

    private CancellationTokenSource? cancellationTokenSource;
    private readonly Lock executeLock = new();

    internal void Reset()
    {
        using (executeLock.EnterScope())
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
        using (executeLock.EnterScope())
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
        using (executeLock.EnterScope())
        {
            if (State == NodeStates.Running && shouldExecute ||
                State != NodeStates.Running && !shouldExecute) return;

            if (cancellationTokenSource is not null)
            {
                // ReSharper disable once MethodHasAsyncOverload
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