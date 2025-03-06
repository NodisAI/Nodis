using Nodis.Interfaces;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Models.Workflow;

/// <summary>
/// Defines a user-defined node in a workflow.
/// </summary>
[YamlObject]
[YamlObjectUnion("!RESTful", typeof(WorkflowRestfulNode))]
public abstract partial class WorkflowUserNode(int id, string name) : WorkflowNode(id)
{
    [YamlMember("name")]
    public override string Name { get; } = name;

    [YamlMember("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [YamlMember("runtimes")]
    public IReadOnlySet<NameAndVersionConstraints> Runtimes { get; init; } = new HashSet<NameAndVersionConstraints>();

    [YamlMember("data_inputs")]
    public IList<WorkflowNodeDataInputPin>? UserDataInputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataInputs.AddRange(field);
        }
    }

    [YamlMember("data_outputs")]
    public IList<WorkflowNodeDataOutputPin>? UserDataOutputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataOutputs.AddRange(field);
        }
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken) =>
        App.Resolve<IEnvironmentManager>().EnsureRuntimesAsync(Namespace, Runtimes, cancellationToken);

    public WorkflowUserNode Clone()
    {
        var options = App.Resolve<YamlSerializerOptions>();
        return YamlSerializer.Deserialize<WorkflowUserNode>(YamlSerializer.Serialize(this, options), options);
    }
}