using Nodis.Core.Models.Workflow;
using VYaml.Annotations;

namespace Nodis.Core.Models;

[YamlObject]
public partial class PackageMetadata
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
    public IReadOnlyList<RuntimeMetadata> Runtimes { get; set; } = [];

    [YamlMember("nodes")]
    public IReadOnlyList<UserNode> Nodes { get; set; } = [];
}