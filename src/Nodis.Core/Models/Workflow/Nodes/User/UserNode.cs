﻿using Nodis.Core.Interfaces;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Nodis.Core.Models.Workflow;

/// <summary>
/// Defines a user-defined node in a workflow.
/// </summary>
public abstract partial class UserNode(string name) : Node
{
    [YamlMember("name")]
    public override string Name { get; } = name;

    [YamlMember("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [YamlMember("runtimes")]
    public IReadOnlySet<NameAndVersionConstraints> Runtimes { get; init; } = new HashSet<NameAndVersionConstraints>();

    [YamlMember("data_inputs")]
    public IList<NodeDataInputPin>? UserDataInputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataInputs.AddRange(field);
        }
    }

    [YamlMember("data_outputs")]
    public IList<NodeDataOutputPin>? UserDataOutputs
    {
        get;
        init
        {
            if ((field = value) == null) return;
            DataOutputs.AddRange(field);
        }
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken) =>
        ServiceLocator.Resolve<IEnvironmentManager>().EnsureRuntimesAsync(Namespace, Runtimes, cancellationToken);

    public UserNode Clone()
    {
        var options = ServiceLocator.Resolve<YamlSerializerOptions>();
        return YamlSerializer.Deserialize<UserNode>(YamlSerializer.Serialize(this, options), options);
    }
}