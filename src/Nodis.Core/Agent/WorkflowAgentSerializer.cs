using System.Diagnostics.CodeAnalysis;
using System.Text;
using Nodis.Core.Extensions;
using Nodis.Core.Models.Workflow;
using VYaml.Parser;

namespace Nodis.Core.Agent;

/// <summary>
/// This class is a bridge between LLM and the actual <see cref="Node"/>.
/// A built-in <see cref="Node"/> can be serialized to YAML.
/// </summary>
public static class WorkflowAgentSerializer
{
    public static string Serialize(Node node, string nodeTag)
    {
        // we use custom StringBuilder instead of YamlEmitter
        // because this should be a human-readable format, for prompt engineering

        var builder = new IndentStringBuilder();
        builder.Append("- ").AppendLine(nodeTag).IncreaseIndent();

        if (node.Id > 0)
        {
            builder.Append("id: ").AppendLine(node.Id.ToString());
        }

        if (!string.IsNullOrWhiteSpace(node.Comment))
        {
            builder.Append("comment: ").AppendLine(node.Comment);
        }

        if (node.IsDataInputsDynamic)
        {
            builder.AppendLine("inputs: (dynamic)").IncreaseIndent();
        }
        else if (node.DataInputs.Count > 0)
        {
            builder.AppendLine("inputs:").IncreaseIndent();
            foreach (var input in node.DataInputs)
            {
                builder.Append(input.Name).Append(": (").Append(input.Data.Type.ToFriendlyString()).Append(") ");
                if (input.Data is NodeEnumData enumData)
                {
                    builder.Append('[').Append(string.Join('|', enumData.Values)).Append("] ");
                }
                builder.AppendLine(input.Description);
            }
        }

        if (node.ControlOutputs.Count > 0)
        {
            builder.DecreaseIndent().AppendLine("pins:").IncreaseIndent();
            foreach (var pin in node.ControlOutputs)
            {
                builder.Append(pin.Name).Append(": ").AppendLine(pin.Description);
            }
        }

        if (node.DataOutputs.Count > 0)
        {
            builder.DecreaseIndent().AppendLine("outputs:").IncreaseIndent();
            foreach (var output in node.DataOutputs)
            {
                builder.Append(output.Name).Append(": (").Append(output.Data.Type.ToFriendlyString()).Append(") ").AppendLine(output.Description);
            }
        }

        return builder.ToString();
    }

    public static WorkflowContext Deserialize(string yaml)
    {
        // We use custom Deserialize for a better error handling
        var errors = new List<Exception>();
        var nodeDefinitions = new Dictionary<int, NodeDefinition>();

        var parser = YamlParser.FromBytes(new Memory<byte>(Encoding.UTF8.GetBytes(yaml)));
        while (parser.Read())
        {
            if (parser.CurrentEventType != ParseEventType.SequenceStart) continue;
            parser.Read(); // Skip SequenceStart

            if (!parser.TryGetCurrentTag(out var tag))
            {
                Throw(parser.CurrentMark, "Tag not found");
                Raise();
            }

            var nodeDefinition = new NodeDefinition(tag.Handle, parser.CurrentMark);
            while (!parser.End && parser.CurrentEventType != ParseEventType.MappingEnd)
            {
                // - !node
                //   id: (required) Unique positive integer
                //   comment: (optional) Human-readable explanation of node functionality
                //   from: (required except first node) Previous node pin reference in @<id>.<pin> format
                //   inputs: (optional) Data inputs - constants or references (@<id>.<output>)
                //   pins: (optional) Activation gates determining subsequent node execution
                //   outputs: (optional) Generated data available for subsequent nodes

                var key = parser.ReadScalarAsString();
                switch (key?.ToLower())
                {
                    case "id":
                    {
                        nodeDefinition.Id = parser.ReadScalarAsInt32();
                        break;
                    }
                    case "comment":
                    {
                        nodeDefinition.Comment = parser.ReadScalarAsString();
                        break;
                    }
                    case "from":
                    {
                        var from = parser.ReadScalarAsString();
                        if (from == null) break;
                        if (!from.StartsWith('@') ||
                            !NodeReference.TryParse(from[1..], parser.CurrentMark, out var reference))
                        {
                            Throw(parser.CurrentMark, "Invalid reference format");
                            break;
                        }
                        nodeDefinition.From = reference;
                        break;
                    }
                    case "inputs":
                    {
                        if (parser.CurrentEventType != ParseEventType.MappingStart)
                        {
                            Throw(parser.CurrentMark, "Invalid inputs format");
                            break;
                        }
                        parser.Read(); // Skip MappingStart

                        while (!parser.End && parser.CurrentEventType != ParseEventType.MappingEnd)
                        {
                            if (parser.ReadScalarAsString() is not { Length: > 0 } inputName)
                            {
                                Throw(parser.CurrentMark, "Invalid input pin name");
                                break;
                            }


                        }
                        break;
                    }
                    case "pins":
                    {
                        if (parser.CurrentEventType != ParseEventType.MappingStart)
                        {
                            Throw(parser.CurrentMark, "Invalid pins format");
                            break;
                        }
                        parser.Read(); // Skip MappingStart

                        while (!parser.End && parser.CurrentEventType != ParseEventType.MappingEnd)
                        {

                        }
                        break;
                    }
                    case "outputs":
                    {
                        if (parser.CurrentEventType != ParseEventType.MappingStart)
                        {
                            Throw(parser.CurrentMark, "Invalid outputs format");
                            break;
                        }
                        parser.Read(); // Skip MappingStart

                        while (!parser.End && parser.CurrentEventType != ParseEventType.MappingEnd)
                        {

                        }
                        break;
                    }
                }
            }

            nodeDefinitions.Add(nodeDefinition.Id, nodeDefinition);
        }

        if (errors.Count > 0) Raise();

        var workflow = new WorkflowContext();

        return workflow;

        void Throw(in Marker marker, string message)
        {
            var builder = new StringBuilder(message)
                .AppendLine()
                .Append("At Line ").Append(marker.Line).Append(", Column ").Append(marker.Col).AppendLine();
            var startLine = Math.Max(marker.Line - 1, 0);
            using var reader = new StreamReader(yaml);
            var line = 0;
            while (line++ < startLine) reader.ReadLine();
            while (line++ < marker.Line) builder.AppendLine(reader.ReadLine());
            builder.Append(reader.ReadLine()).AppendLine(" // <-- Error here");
            while (line++ < startLine + 1) builder.AppendLine(reader.ReadLine());
            errors.Add(new FormatException(builder.Append(reader.ReadLine()).ToString()));
        }

        [DoesNotReturn]
        void Raise() => throw new AggregateException("Errors during deserialization", errors);
    }

    private ref struct IndentStringBuilder(string indent = "  ")
    {
        private readonly StringBuilder builder = new();
        private int indentLevel;
        private bool isStartOfLine = true;

        private void AppendIndentIfNeeded()
        {
            if (!isStartOfLine) return;
            for (var i = 0; i < indentLevel; i++) builder.Append(indent);
            isStartOfLine = false;
        }

        public IndentStringBuilder IncreaseIndent(int count = 1)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count, nameof(count));
            indentLevel += count;
            return this;
        }

        public IndentStringBuilder DecreaseIndent(int count = 1)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count, nameof(count));
            indentLevel -= count;
            return this;
        }

        public IndentStringBuilder Append(char value)
        {
            AppendIndentIfNeeded();
            builder.Append(value);
            return this;
        }

        public IndentStringBuilder Append(string? value)
        {
            AppendIndentIfNeeded();
            builder.Append(value);
            return this;
        }

        public IndentStringBuilder AppendLine(string? value)
        {
            AppendIndentIfNeeded();
            builder.AppendLine(value);
            isStartOfLine = true;
            return this;
        }

        public override string ToString()
        {
            return builder.ToString();
        }
    }

    private abstract record MarkerRecord(Marker Marker);

    /// <summary>
    /// This is a bridge between LLM and the actual <see cref="Node"/>.
    /// </summary>
    /// <param name="Tag"></param>
    private record NodeDefinition(string Tag, Marker Marker) : MarkerRecord(Marker)
    {
        public int Id { get; set; }
        public string? Comment { get; set; }
        public NodeReference? From { get; set; }
        public List<NodeDataInputPin> Inputs { get; } = [];
    }

    private record NodeReference(int Id, string PinName, Marker Marker) : MarkerRecord(Marker)
    {
        public static bool TryParse(string value, Marker marker, out NodeReference? reference)
        {
            var index = value.IndexOf('.');
            if (index < 0 || !int.TryParse(value[..index], out var id))
            {
                reference = null;
                return false;
            }

            reference = new NodeReference(id, value[(index + 1)..], marker);
            return true;
        }
    }
}