using System.Runtime.Serialization;
using VYaml.Annotations;

namespace Nodis.Core.Models;

[YamlObject]
[YamlObjectUnion("!mcp", typeof(McpBundleRuntimeConfiguration))]
[YamlObjectUnion("!api", typeof(ApiBundleRuntimeConfiguration))]
public abstract partial record BundleRuntimeConfiguration
{
    /// <summary>
    /// A unique identifier in the bundle.
    /// </summary>
    [YamlMember("id")]
    public required string Id
    {
        get;
        init => field = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Id cannot be null or empty.") : value;
    }

    [YamlMember("type")]
    public BundleRuntimeType Type { get; init; } = BundleRuntimeType.ServerSide;

    [YamlMember("pre_installs")]
    public IReadOnlyList<RuntimeInstallOperation>? PreInstalls { get; init; }

    [YamlMember("post_installs")]
    public IReadOnlyList<RuntimeInstallOperation>? PostInstalls { get; init; }
}

public enum BundleRuntimeType
{
    /// <summary>
    /// The runtime runs on the server. This is the most common case. For example, a timer is running on server and starts a scrapy job.
    /// </summary>
    [EnumMember(Value = "server-side")]
    ServerSide,

    /// <summary>
    /// The runtime runs on the client. This is the case when a user is running a job on their local machine.
    /// For example, getting a list of files from a local directory.
    /// </summary>
    [EnumMember(Value = "client-side")]
    ClientSide,
}

#region MCP

[YamlObject]
public partial record McpBundleRuntimeConfiguration : BundleRuntimeConfiguration
{
    [YamlMember("transport")]
    public required McpTransportConfiguration TransportConfiguration { get; init; }
}

[YamlObject]
[YamlObjectUnion("!stdio", typeof(StdioMcpTransportConfiguration))]
[YamlObjectUnion("!sse", typeof(SseMcpTransportConfiguration))]
public abstract partial record McpTransportConfiguration;

[YamlObject]
public partial record StdioMcpTransportConfiguration(
    [property: YamlMember("command")] string Command,
    [property: YamlMember("args")] IReadOnlyList<string> Arguments,
    [property: YamlMember("workdir")] string? WorkingDirectory = null,
    [property: YamlMember("env")] IReadOnlyDictionary<string, ValueWithDescription<string>>? EnvironmentVariables = null) : McpTransportConfiguration;

[YamlObject]
public partial record SseMcpTransportConfiguration(
    [property: YamlMember("url")] string Url,
    [property: YamlMember("headers")] IReadOnlyDictionary<string, ValueWithDescription<string>>? Headers = null) : McpTransportConfiguration;

#endregion

#region API

[YamlObject]
public partial record ApiBundleRuntimeConfiguration : BundleRuntimeConfiguration
{
    [YamlMember("transport")]
    public required ApiTransportConfiguration TransportConfiguration { get; init; }
}

[YamlObject]
[YamlObjectUnion("!rest", typeof(RestApiTransportConfiguration))]
public abstract partial record ApiTransportConfiguration;

[YamlObject]
public partial record RestApiTransportConfiguration(
    [property: YamlMember("url")] string Url,
    [property: YamlMember("headers")] IReadOnlyDictionary<string, ValueWithDescription<string>>? Headers = null) : ApiTransportConfiguration;

#endregion