using Nodis.Core.Models.Workflow;
using VYaml.Annotations;

namespace Nodis.Core.Models;

/// <summary>
/// Marketplace lists <see cref="BundleManifest"/>, after install, it will be <see cref="InstalledBundle"/> and saved in local disk.
/// </summary>
[YamlObject]
public partial record InstalledBundle(
    [property: YamlMember("manifest")] BundleManifest Manifest,
    [property: YamlMember("nodes")] IReadOnlyList<UserNode> Nodes);