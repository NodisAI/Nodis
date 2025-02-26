using System.Runtime.Serialization;
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

    [YamlMember("source")]
    public required NodeSource Source { get; set; }

    [YamlMember("pre_install")]
    public NodeInstallOperation[] PreInstall { get; set; } = [];

    [YamlMember("post_install")]
    public NodeInstallOperation[] PostInstall { get; set; } = [];
}

public enum NodeInstallOperationType
{
    [EnumMember(Value = "script")]
    Script,
    [EnumMember(Value = "bash")]
    Bash
}

[YamlObject]
public partial class NodeInstallOperation
{
    [YamlMember("type")]
    public required NodeInstallOperationType Type { get; set; }

    [YamlMember("name")]
    public string? Name { get; set; }

    [YamlMember("args")]
    public required string Args { get; set; }
}

[YamlObject]
public partial class NodeSource
{
    [YamlMember("type")]
    public required string Type { get; set; }

    [YamlMember("url")]
    public required string Url { get; set; }

    [YamlMember("commit")]
    public string? Commit { get; set; }
}