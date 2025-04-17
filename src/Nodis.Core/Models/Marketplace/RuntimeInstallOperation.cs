using VYaml.Annotations;

namespace Nodis.Core.Models;

[YamlObject]
[YamlObjectUnion("!bash", typeof(BashRuntimeInstallOperation))]
[YamlObjectUnion("!git", typeof(GitRuntimeInstallOperation))]
public abstract partial record RuntimeInstallOperation;

[YamlObject]
public partial record BashRuntimeInstallOperation(
    [property: YamlMember("commands")] IReadOnlyList<string> CommandLines
) : RuntimeInstallOperation;

[YamlObject]
public partial record GitRuntimeInstallOperation(
    [property: YamlMember("url")] string Url,
    [property: YamlMember("branch")] string? Branch = null,
    [property: YamlMember("tag")] string? Tag = null,
    [property: YamlMember("commit")] string? Commit = null
) : RuntimeInstallOperation;
