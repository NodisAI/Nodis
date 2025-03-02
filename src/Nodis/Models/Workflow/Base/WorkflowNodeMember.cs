using CommunityToolkit.Mvvm.ComponentModel;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowNodeMember(string name) : ObservableObject
{
    [YamlMember("id")]
    public int Id { get; internal set; }

    [YamlMember("name")]
    public string Name => name;

    [YamlIgnore]
    public WorkflowNode? Owner { get; internal set; }
}