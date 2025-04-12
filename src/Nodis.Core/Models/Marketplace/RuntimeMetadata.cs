using System.Runtime.Serialization;
using VYaml.Annotations;

namespace Nodis.Core.Models;

[YamlObject]
[YamlObjectUnion("!git", typeof(GitRuntimeMetadata))]
[YamlObjectUnion("!executable_bundle", typeof(ExecutableBundleRuntimeMetadata))]
public abstract partial record RuntimeMetadata(
    [property: YamlMember("id")] string Id)
{
    [YamlMember("pre_install")]
    public IReadOnlyList<RuntimeInstallOperation> PreInstalls { get; set; } = [];

    [YamlMember("post_install")]
    public IReadOnlyList<RuntimeInstallOperation> PostInstalls { get; set; } = [];

    /// <summary>
    /// This property is used for managing the persist folder paths of the runtime.
    /// </summary>
    [YamlMember("persist")]
    public IReadOnlyList<string> PersistFolderPaths { get; set; } = [];
}

[YamlObject]
public partial record GitRuntimeMetadata(
    string Id,
    [property: YamlMember("url")] string Url,
    [property: YamlMember("commit")] string Commit
) : RuntimeMetadata(Id);

[YamlObject]
public partial record ExecutableBundleRuntimeMetadata(
    string Id,
    [property: YamlMember("distributions")] IReadOnlyDictionary<string, CompressedExecutableNodeServiceDistribution> Distributions
) : RuntimeMetadata(Id);

public enum CompressedExecutableNodeServiceDistributionType
{
    [EnumMember(Value = "zip")]
    Zip,
    [EnumMember(Value = "tar")]
    Tar,
    [EnumMember(Value = "tgz")]
    Tgz,
}

[YamlObject]
public partial record CompressedExecutableNodeServiceDistribution(
    [property: YamlMember("url")] string Url,
    [property: YamlMember("type")] CompressedExecutableNodeServiceDistributionType Type,
    [property: YamlMember("checksum")] Checksum Checksum,
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