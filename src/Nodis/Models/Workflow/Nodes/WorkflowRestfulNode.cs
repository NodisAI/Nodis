using System.Text.Json;
using System.Text.RegularExpressions;
using Json.More;
using Json.Path;
using VYaml.Annotations;

namespace Nodis.Models.Workflow;

[YamlObject]
public partial class WorkflowRestfulNode : WorkflowUserNode
{
    [YamlMember("request")]
    public required WorkflowRestfulNodeRequest Request { get; init; }

    [YamlMember("response")]
    public required WorkflowRestfulNodeResponse Response { get; init; }

    public WorkflowRestfulNode(string name) : base(name)
    {
        ControlInput = new WorkflowNodeControlInputPin();
        ControlOutputs.Add(new WorkflowNodeControlOutputPin("Success"));
        ControlOutputs.Add(new WorkflowNodeControlOutputPin("Failure"));
        DataOutputs.Add(new WorkflowNodeDataOutputPin("Status code", new WorkflowNodeIntegerData()));
    }

    protected override async Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        await base.ExecuteImplAsync(cancellationToken);

        bool result;
        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Parse(Request.Method), Request.Url);
            if (Request.Headers is not null)
            {
                foreach (var (key, value) in Request.Headers)
                {
                    requestMessage.Headers.Add(ExpandVariables(key), ExpandVariables(value));
                }
            }
            if (Request.Body is not null)
            {
                requestMessage.Content = new StringContent(ExpandVariables(Request.Body));
            }

            var responseMessage = await App.Resolve<HttpClient>().SendAsync(requestMessage, cancellationToken);
            DataOutputs[0].Data.Value = responseMessage.StatusCode;
            responseMessage.EnsureSuccessStatusCode(); // todo: validation rules in yaml

            if (Response.BodyMapping != null)
            {
                await using var stream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);
                switch (responseMessage.Content.Headers.ContentType?.MediaType)
                {
                    case "application/json":
                    {
                        var jsonDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                        foreach (var (id, path) in Response.BodyMapping)
                        {
                            var port = DataOutputs.FirstOrDefault(p => p.Id == id) ??
                                throw new InvalidOperationException($"DataOutputs[{id}] is not found.");
                            var value = JsonPath.Parse(path).Evaluate(jsonDocument.RootElement.AsNode()).Matches.FirstOrDefault()?.Value?.AsValue();
                            if (value == null)
                            {
                                port.Data.Value = null;
                                break;
                            }

                            switch (port.Data.Type)
                            {
                                case WorkflowNodeDataType.Boolean:
                                {
                                    port.Data.Value = value.GetBool();
                                    break;
                                }
                                case WorkflowNodeDataType.Integer:
                                {
                                    port.Data.Value = value.GetInteger();
                                    break;
                                }
                                case WorkflowNodeDataType.Float:
                                {
                                    port.Data.Value = value.GetNumber();
                                    break;
                                }
                                case WorkflowNodeDataType.Text:
                                {
                                    port.Data.Value = value.GetString();
                                    break;
                                }
                                case WorkflowNodeDataType.DateTime:
                                {
                                    if (value.GetString() is { } str) port.Data.Value = DateTime.Parse(str);
                                    else if (value.GetInteger() is { } ticks) port.Data.Value = new DateTime(ticks);
                                    else throw new InvalidOperationException("Invalid DateTime value.");
                                    break;
                                }
                                default:
                                {
                                    throw new NotImplementedException();
                                }
                            }
                        }
                        break;
                    }
                    case "application/xml":
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            result = true;
        }
        catch
        {
            result = false;
        }

        ControlOutputs[0].CanExecute = result;
        ControlOutputs[1].CanExecute = !result;
    }

    /// <summary>
    /// replace all $id in input with the corresponding pin in DataInputs. \$id will be escaped.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private string ExpandVariables(string input) => ExpandVariablesRegex().Replace(input, match =>
    {
        var id = int.Parse(match.Groups[1].Value);
        var port = DataInputs.FirstOrDefault(p => p.Id == id) ??
            throw new InvalidOperationException($"DataInputs[{id}] is not found.");
        return port.Value?.ToString() ?? string.Empty;
    }).Replace("\\$", "$");

    [GeneratedRegex(@"(?<!\\)\$(\d+)", RegexOptions.Compiled)]
    private static partial Regex ExpandVariablesRegex();
}

[YamlObject]
public partial record WorkflowRestfulNodeRequest(
    [property: YamlMember("method")] string Method,
    [property: YamlMember("url")] string Url,
    [property: YamlMember("headers")] IReadOnlyDictionary<string, string>? Headers = null,
    [property: YamlMember("body")] string? Body = null);

[YamlObject]
public partial record WorkflowRestfulNodeResponse(
    [property: YamlMember("body_mapping")] IReadOnlyDictionary<int, string>? BodyMapping = null);