using VYaml.Annotations;

namespace Nodis.Core.Models;

/// <summary>
/// A bundle represents a collection of nodes and their metadata.
/// It contains one or more runtimes.
/// Runtime provides the actual implementation of the node.
/// With the power of MCP, we can get nodes of each runtime and call them.
/// </summary>
[YamlObject]
public partial record BundleManifest
{
    /// <summary>
    /// A user-friendly name of the bundle. This is also used for LLM and search.
    /// </summary>
    [YamlMember("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Url to the icon of the bundle.
    /// </summary>
    [YamlMember("icon")]
    public string? Icon { get; init; }

    /// <summary>
    /// Url to the readme of the bundle.
    /// </summary>
    [YamlMember("readme")]
    public string? Readme { get; init; }

    /// <summary>
    /// Url to the homepage of the bundle.
    /// </summary>
    [YamlMember("homepage")]
    public string? Homepage { get; init; }

    /// <summary>
    /// License of the bundle.
    /// </summary>
    /// <remarks>
    /// Can be an url to the license file or a SPDX license identifier. e.g. "MIT", "Apache-2.0", "https://opensource.org/licenses/MIT"
    /// </remarks>
    [YamlMember("license")]
    public required string License { get; init; }

    [YamlMember("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    [YamlMember("runtimes")]
    public required IReadOnlyList<BundleRuntimeConfiguration> Runtimes
    {
        get;
        init
        {
            if (value.DistinctBy(r => r.Id, StringComparer.OrdinalIgnoreCase).Count() != value.Count)
                throw new ArgumentException("Duplicate runtime ids found in the bundle manifest (Case insensitive).");
            field = value;
        }
    }
}