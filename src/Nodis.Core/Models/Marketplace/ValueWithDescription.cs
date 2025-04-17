using CommunityToolkit.Mvvm.ComponentModel;
using VYaml.Annotations;

namespace Nodis.Core.Models;

public abstract class ValueWithDescriptionBase : ObservableObject
{
    [YamlMember("description")]
    public required string Description { get; set; }
}

[YamlObject]
public partial class ValueWithDescription<T> : ValueWithDescriptionBase
{
    [ObservableProperty]
    [YamlMember("default")]
    public partial T? Value { get; set; }
}