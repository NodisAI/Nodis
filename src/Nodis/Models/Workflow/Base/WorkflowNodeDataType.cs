using System.Runtime.Serialization;

namespace Nodis.Models.Workflow;

public enum WorkflowNodeDataType
{
    [EnumMember(Value = "bool")]
    Boolean,
    [EnumMember(Value = "int")]
    Integer,
    [EnumMember(Value = "float")]
    Float,
    [EnumMember(Value = "str")]
    Text,
    [EnumMember(Value = "datetime")]
    DateTime,
    [EnumMember(Value = "seq")]
    List,
    [EnumMember(Value = "map")]
    Dictionary,
    [EnumMember(Value = "any")]
    Any
}