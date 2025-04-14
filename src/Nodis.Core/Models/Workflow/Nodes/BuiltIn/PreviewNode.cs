using MessagePack;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
public partial class PreviewNode : BuiltInNode
{
    [YamlIgnore]
    [IgnoreMember]
    public override string Name => "Preview";

    [YamlIgnore]
    [IgnoreMember]
    public object? FooterContent => DataInputs[0].Value;

    public PreviewNode()
    {
        ControlInput = new NodeControlInputPin();
        DataInputs.Add(new NodeDataInputPin("data", new NodeAnyData()));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        OnPropertyChanged(nameof(FooterContent));
        return Task.CompletedTask;
    }
}