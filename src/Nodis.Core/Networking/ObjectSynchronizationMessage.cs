using MessagePack;

namespace Nodis.Core.Networking;

[MessagePackObject]
[Union(0, typeof(ObjectSynchronizationPropertyMessage))]
public abstract partial class ObjectSynchronizationMessage
{
    [Key(0)] public long Timestamp { get; set; } = DateTime.UtcNow.Ticks;
    [Key(1)] public required Guid ObjectId { get; init; }
    [Key(2)] public required uint Version { get; init; }
}

[MessagePackObject]
public partial class ObjectSynchronizationPropertyMessage : ObjectSynchronizationMessage
{
    [Key(3)] public List<(string PropertyName, object? Value)> Properties { get; set; } = new();
}