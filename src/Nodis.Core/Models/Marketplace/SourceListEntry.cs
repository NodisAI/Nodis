using VYaml.Annotations;

namespace Nodis.Core.Models;

/// <summary>
/// Like sources.list in apt. sources.yaml is a list of <see cref="SourceListEntry"/>
/// </summary>
/// <remarks>
/// $(UserProfile)/nodis/sources/index.yaml
/// </remarks>
[YamlObject]
public partial record SourceListEntry(
    [property: YamlMember("url")] string Url,
    [property: YamlMember("namespace")] string Namespace)
{
    public static SourceListEntry Main => new("https://github.com/NodisAI/Main", "NodisAI.Main");
}