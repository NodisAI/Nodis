using CommunityToolkit.Mvvm.ComponentModel;
using MessagePack;
using Nodis.Core.Networking;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[MessagePackObject(AllowPrivate = true)]
[Union(0, typeof(NodeControlInputPin))]
[Union(1, typeof(NodeControlOutputPin))]
[Union(2, typeof(NodeDataInputPin))]
[Union(3, typeof(NodeDataOutputPin))]
[Union(4, typeof(NodeProperty))]
public abstract partial class NodeMember : ObservableObject
{
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
    public ulong Id { get; internal set; }

    [YamlMember("name")]
    [Key(2)]
    public string Name { get; set; }

    [YamlMember("description")]
    [Key(3)]
    public string? Description { get; set; }

    [YamlIgnore]
    [IgnoreMember]
    public Node? Owner { get; internal set; }

    protected NodeMember(string name)
    {
        Name = name;
        tracker = new NetworkObjectTracker(this);
    }
}