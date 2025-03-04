using System.Runtime.Serialization;
using VYaml.Annotations;

namespace Nodis.Models;

[YamlObject]
[YamlObjectUnion("!git", typeof(GitNodeRuntime))]
[YamlObjectUnion("!executable_bundle", typeof(ExecutableBundleNodeRuntime))]
public abstract partial record NodeRuntime(
    [property: YamlMember("id")] string Id);

[YamlObject]
public partial record GitNodeRuntime(
    string Id,
    [property: YamlMember("url")] string Url,
    [property: YamlMember("commit")] string Commit
) : NodeRuntime(Id);

[YamlObject]
public partial record ExecutableBundleNodeRuntime(
    string Id,
    [property: YamlMember("distributions")] IReadOnlyDictionary<string, CompressedExecutableNodeServiceDistribution> Distributions
) : NodeRuntime(Id);

public enum CompressedExecutableNodeServiceDistributionType
{
    [EnumMember(Value = "zip")]
    Zip,
    [EnumMember(Value = "tgz")]
    Tgz,
}

[YamlObject]
public partial record CompressedExecutableNodeServiceDistribution(
    [property: YamlMember("url")] string Url,
    [property: YamlMember("type")] CompressedExecutableNodeServiceDistributionType Type,
    [property: YamlMember("checksum")] string Checksum,
    [property: YamlMember("execution")] CompressedExecutableNodeSourcePlatformExecution Execution);

public enum CompressedExecutableNodeSourcePlatformStartupLifecycle
{
    [EnumMember(Value = "singleton")]
    Singleton,
    [EnumMember(Value = "transient")]
    Transient
}

[YamlObject]
public partial record CompressedExecutableNodeSourcePlatformExecution(
    [property: YamlMember("lifecycle")] CompressedExecutableNodeSourcePlatformStartupLifecycle Lifecycle,
    [property: YamlMember("command")] string Command);