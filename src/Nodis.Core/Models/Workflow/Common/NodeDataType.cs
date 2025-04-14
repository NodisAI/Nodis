using System.Runtime.Serialization;

namespace Nodis.Core.Models.Workflow;

public enum NodeDataType
{
    [EnumMember(Value = "any")]
    Object,
    [EnumMember(Value = "boolean")]
    Boolean,
    [EnumMember(Value = "integer")]
    Int64,
    [EnumMember(Value = "decimal")]
    Double,
    [EnumMember(Value = "text")]
    String,
    [EnumMember(Value = "datetime")]
    DateTime,
    [EnumMember(Value = "enum")]
    Enum,
    [EnumMember(Value = "sequence")]
    Enumerable,
    [EnumMember(Value = "dictionary")]
    Dictionary,
    [EnumMember(Value = "binary")]
    Stream
}