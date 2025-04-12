using VYaml.Annotations;

namespace Nodis.Core.Models;

[YamlObject]
[YamlObjectUnion("!script", typeof(ScriptRuntimeInstallOperation))]
[YamlObjectUnion("!bash", typeof(BashRuntimeInstallOperation))]
public abstract partial record RuntimeInstallOperation
{
    [YamlMember("env_add_path")]
    public IReadOnlyList<string>? EnvironmentAddPath { get; init; }
}

[YamlObject]
public partial record ScriptRuntimeInstallOperation(
    [property: YamlMember("name")] string Name,
    [property: YamlMember("args")] string Args
) : RuntimeInstallOperation;

[YamlObject]
public partial record BashRuntimeInstallOperation(
    [property: YamlMember("command")] string Command
) : RuntimeInstallOperation;

