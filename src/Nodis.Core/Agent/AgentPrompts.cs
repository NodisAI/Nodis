using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using Nodis.Core.Extensions;
using Nodis.Core.Models.Workflow;
using VYaml.Annotations;

namespace Nodis.Core.Agent;

public record WorkflowPromptOptions(
    string Task,
    bool EnableBuiltInNodes = true,
    bool EnableThirdPartyNodes = true,
    bool EnableFunctionNodes = true,
    CultureInfo? CultureInfo = null
);

public static class AgentPrompts
{
    [field: AllowNull, MaybeNull]
    private static string BuiltInNodesYaml
    {
        get
        {
            if (field != null) return field;
            var builder = new StringBuilder();
            foreach (var attribute in typeof(BuiltInNode).GetCustomAttributes<YamlObjectUnionAttribute>())
            {
                var node = Activator.CreateInstance(attribute.SubType).NotNull<BuiltInNode>();
                builder.AppendLine(WorkflowAgentSerializer.Serialize(node, attribute.Tag));
            }
            return field = builder.ToString();
        }
    }

    /// <summary>
    /// Get a prompt for the workflow agent.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public static string GetWorkflowPrompt(WorkflowPromptOptions options)
    {
        var cultureInfo = options.CultureInfo ?? CultureInfo.CurrentUICulture;

        var builder = new StringBuilder();
        builder.AppendLine("# Task").AppendLine(options.Task);
        builder.AppendLine(
            $"""

            # Description
            A workflow is a low-code programming structure composed of nodes.
            It allows amateur developers, referring to individuals with minimal or no formal programming training, to achieve programming objectives with much lower learning costs and effort.
            Each node follows this base schema:
            ```yaml
            - !node
              id: (required) Unique positive integer
              comment: (optional) Human-readable explanation of node functionality in {cultureInfo.EnglishName}
              from: (required except first node) Previous node pin reference in @<id>.<pin> format
              inputs: (optional) Data inputs - constants or references (@<id>.<output>)
              pins: Activation gates determining subsequent node execution, do not include in your output
              outputs: (optional) Generated data available for subsequent nodes
            ```

            Nodes activate sequentially through `pins`-`from` connections
            1. First node executes core logic, then corresponding pin activates next node(s)
            2. When a node is activated, it starts resolving referenced `inputs` from upstream `outputs` (if any); for type mismatches, the system uses C# `Convert` class for type conversion
            3. One pin can activates multiple nodes, but one node cannot be activated by multiple pins (`from` cannot references multiple nodes))

            # Data types
            - `any`: can be converted between any type
            - `boolean`: true or false
            - `integer`: 64-bit signed integer
            - `decimal`: 64-bit floating-point number
            - `text`: UTF-8 string
            - `datetime`: Date and time in ISO 8601 format
            - `enum`: similar to text, but holds a set of case-insensitive text as values
            - `sequence`: list of any type
            - `dictionary`: unordered key-value dictionary, key is text and value is any type
            - `binary`: binary stream
            """);

        if (options.EnableBuiltInNodes)
        {
            builder.AppendLine(
                """

                # Built-in Nodes 
                ```yaml
                """
            ).AppendLine(BuiltInNodesYaml).AppendLine("```");
        }

        if (options.EnableThirdPartyNodes)
        {
            builder.AppendLine(
                """

                # Third-party Nodes
                There are hundreds of third-party nodes, you can use `search_node` tool to search them
                """);
        }

        if (options.EnableFunctionNodes)
        {
            builder.AppendLine(
                """

                # Function Nodes
                If you cannot find a suitable node, or the logic is too complex to be expressed in a single workflow, or you need to reuse the logic in multiple workflows, you can use `create_function` tool to enable another agent to create a custom function for you, then use it just like a normal node
                """);
        }

        builder.Append(
            """

            # Generation Rules
            1. Maintain sequential ID numbering starting from 1
            2. All references must resolve to existing node. `from` can only reference to one of `pins`, `inputs` can only reference to one of `outputs`
            3. `inputs` and `outputs` are fixed by default, you cannot modify unless it marked as dynamic
            4. Your output should be a valid YAML list of nodes, or valid tool calls format
            """);

        if (options.EnableThirdPartyNodes)
        {
            builder.AppendLine("5. Try search and use third-party nodes as much as possible before output");
        }

        return builder.ToString();
    }
}