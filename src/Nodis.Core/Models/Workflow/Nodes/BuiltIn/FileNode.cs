using System.Runtime.Serialization;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class FileNode : BuiltInNode
{
    [YamlIgnore]
    public override string Name => "File";

    public FileNode()
    {
        ControlInput = new NodeControlInputPin();
        DataInputs.Add(new NodeDataInputPin("action", new NodeEnumData(typeof(FileNodeAction))));
        DataInputs.Add(
            new NodeDataInputPin("path", new NodeStringData(string.Empty))
            {
                Description = "Must be a absolute path"
            });
        DataInputs.Add(
            new NodeDataInputPin("data", new NodeStringData(string.Empty))
            {
                Description = "Data to write",
                Condition = new NodePinValueCondition("action", d => d.Value is FileNodeAction.Write or FileNodeAction.Append)
            });
        ControlOutputs.Add(new NodeControlOutputPin("success"));
        ControlOutputs.Add(new NodeControlOutputPin("failure"));
        DataOutputs.Add(
            new NodeDataOutputPin("result", new NodeStreamData([]))
            {
                Description = "Data read"
            });
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public enum FileNodeAction
{
    [EnumMember(Value = "read")]
    Read,
    [EnumMember(Value = "write")]
    Write,
    [EnumMember(Value = "append")]
    Append,
    [EnumMember(Value = "delete")]
    Delete
}