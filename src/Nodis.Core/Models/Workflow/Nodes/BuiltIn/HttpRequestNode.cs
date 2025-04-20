using MessagePack;
using Nodis.Core.Extensions;
using VYaml.Annotations;

namespace Nodis.Core.Models.Workflow;

[YamlObject]
[MessagePackObject(AllowPrivate = true)]
public partial class HttpRequestNode : BuiltInNode
{
    [YamlIgnore]
    [IgnoreMember]
    public override string Name => "URL Request";

    public HttpRequestNode()
    {
        ControlInput = new NodeControlInputPin();
        DataInputs.Add(new NodeDataInputPin("URL", new NodeStringData(string.Empty)
        {
            Watermark = "https://example.com"
        }));
        DataInputs.Add(new NodeDataInputPin("method", new NodeStringData("GET"))
        {
            Description = "GET, POST, etc."
        });
        DataInputs.Add(new NodeDataInputPin("request Headers", new NodeDictionaryData(new Hashtable())));
        DataInputs.Add(new NodeDataInputPin("request Body", new NodeAnyData()));
        ControlOutputs.Add(new NodeControlOutputPin("success"));
        ControlOutputs.Add(new NodeControlOutputPin("failure"));
        DataOutputs.Add(new NodeDataOutputPin("status Code", new NodeInt64Data(0)));
        DataOutputs.Add(new NodeDataOutputPin("response Headers", new NodeDictionaryData(new Hashtable())));
        DataOutputs.Add(new NodeDataOutputPin("response Body", new NodeAnyData()));
    }

    protected override Task ExecuteImplAsync(CancellationToken cancellationToken)
    {
        var requestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(DataInputs["URL"].Value.NotNull<string>()),
            Method = new HttpMethod(DataInputs["Method"].Value.NotNull<string>())
        };
        foreach (DictionaryEntry entry in DataInputs["Request Headers"].Value.NotNull<IDictionary>())
        {
            if (entry.Key.ToString() is { } key) requestMessage.Headers.Add(key, entry.Value?.ToString());
        }
        var requestBodyPin = DataInputs["Request Body"];
        if (requestBodyPin.Data.Type == NodeDataType.Stream)
        {
            var content = requestBodyPin.Value.NotNull<byte[]>();
            requestMessage.Content = new ByteArrayContent(content);
        }
        else if (requestBodyPin.Value?.ToString() is { } content)
        {
            requestMessage.Content = new StringContent(content);
        }

        throw new NotImplementedException("HTTP request execution is not implemented yet.");
    }
}