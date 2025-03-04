using VYaml.Annotations;

namespace Nodis.Models;

[YamlObject]
[YamlObjectUnion("!script", typeof(ScriptNodeInstallOperation))]
[YamlObjectUnion("!bash", typeof(BashNodeInstallOperation))]
public abstract partial record NodeInstallOperation
{
    [YamlMember("env_add_path")]
    public IReadOnlyList<string>? EnvironmentAddPath { get; init; }
}

[YamlObject]
public partial record ScriptNodeInstallOperation(
    [property: YamlMember("name")] string Name,
    [property: YamlMember("args")] string Args
) : NodeInstallOperation;

[YamlObject]
public partial record BashNodeInstallOperation(
    [property: YamlMember("command")] string Command
) : NodeInstallOperation;

