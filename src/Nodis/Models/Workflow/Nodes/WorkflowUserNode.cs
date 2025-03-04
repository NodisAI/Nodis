using Nodis.Interfaces;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

/// <summary>
/// Defines a user-defined node in a workflow.
/// </summary>
[YamlObject]
[YamlObjectUnion("!RESTful", typeof(WorkflowRestfulNode))]
public abstract partial class WorkflowUserNode(string name) : WorkflowNode
{
    [YamlMember("name")]
    public override string Name { get; } = name;

    [YamlMember("runtimes")]
    public IReadOnlyList<NameAndVersionConstraint> Runtimes { get; init; } = [];

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken) =>
        App.Resolve<IRuntimeHost>().EnsureRuntimesAsync(Runtimes, cancellationToken);
}