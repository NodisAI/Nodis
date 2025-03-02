using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public readonly partial record struct WorkflowNodePortConnection(
    [property: YamlMember("on")] int OutputNodeId,
    [property: YamlMember("op")] int OutputPortId,
    [property: YamlMember("in")] int InputNodeId,
    [property: YamlMember("ip")] int InputPortId);