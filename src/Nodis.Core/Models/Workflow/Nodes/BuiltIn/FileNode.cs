using System.Runtime.Serialization;
using Nodis.Core.Extensions;
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
        DataInputs.Add(new NodeDataInputPin("action", NodeEnumData.FromEnum<FileNodeAction>()));
        DataInputs.Add(
            new NodeDataInputPin("path", new NodeStringData(string.Empty))
            {
                Description = "Must be a absolute path"
            });
        DataInputs.Add(
            new NodeDataInputPin("data", new NodeStreamData([]))
            {
                Description = "Data to write",
                Condition = new NodePinValueCondition(
                    "action",
                    d => d.Value?.ToString()?.ToEnum<FileNodeAction>() is FileNodeAction.Write or FileNodeAction.Append)
            });
        ControlOutputs.Add(new NodeControlOutputPin("success"));
        ControlOutputs.Add(new NodeControlOutputPin("failure"));
        DataOutputs.Add(
            new NodeDataOutputPin("result", new NodeStreamData([]))
            {
                Description = "Data read"
            });
    }

    protected override async Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        try
        {
            var path = DataInputs["path"].Value?.ToString();
            switch (DataInputs["action"].Value?.ToString()?.ToEnum<FileNodeAction>())
            {
                case FileNodeAction.Read:
                {
                    if (path == null) return;
                    DataOutputs["result"].Data.Value = File.OpenRead(path);
                    break;
                }
                case FileNodeAction.Write:
                {
                    if (path == null) return;
                    if (DataInputs["data"].Value is not Stream data) return;
                    await using var fs = File.Create(path);
                    await data.CopyToAsync(fs, cancellationToken);
                    break;
                }
                case FileNodeAction.Append:
                {
                    if (path == null) return;
                    if (DataInputs["data"].Value is not Stream data) return;
                    await using var fs = File.Open(path, FileMode.Append);
                    await data.CopyToAsync(fs, cancellationToken);
                    break;
                }
                case FileNodeAction.Delete:
                {
                    if (path == null) return;
                    File.Delete(path);
                    break;
                }
            }
        }
        catch
        {
            ControlOutputs["success"].CanExecute = false;
            ControlOutputs["failure"].CanExecute = true;
            throw;
        }

        ControlOutputs["success"].CanExecute = true;
        ControlOutputs["failure"].CanExecute = false;
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