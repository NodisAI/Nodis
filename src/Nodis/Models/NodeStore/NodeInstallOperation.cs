using VYaml.Annotations;

namespace Nodis.Models;

[YamlObject]
[YamlObjectUnion("!script", typeof(ScriptNodeInstallOperation))]
[YamlObjectUnion("!bash", typeof(BashNodeInstallOperation))]
public partial interface INodeInstallOperation;

[YamlObject]
public partial record ScriptNodeInstallOperation(
    [property: YamlMember("name")] string Name,
    [property: YamlMember("args")] string Args
) : INodeInstallOperation;

[YamlObject]
public partial record BashNodeInstallOperation(
    [property: YamlMember("command")] string Command
) : INodeInstallOperation;

