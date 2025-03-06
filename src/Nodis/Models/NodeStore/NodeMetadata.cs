using Nodis.Models.Workflow;
using VYaml.Annotations;

namespace Nodis.Models;

[YamlObject]
public partial class NodeMetadata
{
    [YamlMember("description")]
    public required string Description { get; set; }

    [YamlMember("icon")]
    public string? Icon { get; set; }

    [YamlMember("readme")]
    public string? Readme { get; set; }

    [YamlMember("homepage")]
    public string? Homepage { get; set; }

    [YamlMember("license")]
    public required string License { get; set; }

    [YamlMember("runtimes")]
    public required IReadOnlyList<NodeRuntime> Runtimes { get; set; }

    [YamlMember("pre_install")]
    public IReadOnlyList<NodeInstallOperation> PreInstall { get; set; } = [];

    [YamlMember("post_install")]
    public IReadOnlyList<NodeInstallOperation> PostInstall { get; set; } = [];

    [YamlMember("nodes")]
    public IReadOnlyList<WorkflowUserNode> Nodes { get; set; } = [];
}