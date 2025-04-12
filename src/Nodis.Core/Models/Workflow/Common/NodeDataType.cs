using System.Runtime.Serialization;

namespace Nodis.Core.Models.Workflow;

public enum NodeDataType
{
    [EnumMember(Value = "any")]
    Object,
    [EnumMember(Value = "bool")]
    Boolean,
    [EnumMember(Value = "int")]
    Int64,
    [EnumMember(Value = "float")]
    Double,
    [EnumMember(Value = "str")]
    String,
    [EnumMember(Value = "datetime")]
    DateTime,
    [EnumMember(Value = "enum")]
    Enum,
    [EnumMember(Value = "seq")]
    Enumerable,
    [EnumMember(Value = "map")]
    Dictionary,
    [EnumMember(Value = "bin")]
    Stream
}