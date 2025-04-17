#pragma warning disable SKEXP0010
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Nodis.Core.Extensions;

namespace Nodis.Core.Agent;

public static partial class WorkflowAgent
{
    public static async Task RunAsync()
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: "gpt-4o",
            apiKey: Environment.GetEnvironmentVariable("NODIS_API_KEY", EnvironmentVariableTarget.User).NotNull("NODIS_API_KEY is not set")
        );
        builder.Services.AddSingleton<ILoggerFactory, ConsoleLoggerFactory>();
        builder.Plugins.AddFromType<WorkflowTools>();
        var kernel = builder.Build();

        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(
            AgentPrompts.GetWorkflowPrompt(
                new WorkflowPromptOptions(
                    "You are a professional workflow generator. You should follow following requirements to write a workflow in YAML format.",
                    EnableFunctionNodes: false,
                    EnableThirdPartyNodes: false)));
        chatHistory.AddUserMessage("为了测试，请你自己提出一个复杂的需求，思考其实现并生成对应的工作流");

        var promptExecutionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false),
            // Temperature = 0.3d,
            // TopP = 0.95d
        };

        while (true)
        {
            AuthorRole? authorRole = null;
            var assistantContentBuilder = new StringBuilder();
            var functionCallContentBuilder = new FunctionCallContentBuilder();

            await foreach (var streamingContent in chatCompletion.GetStreamingChatMessageContentsAsync(chatHistory, promptExecutionSettings, kernel))
            {
                if (streamingContent.Content is not null)
                {
                    assistantContentBuilder.Append(streamingContent.Content);
                    Console.Write(streamingContent.Content);
                }

                authorRole ??= streamingContent.Role;
                functionCallContentBuilder.Append(streamingContent);
            }

            chatHistory.AddAssistantMessage(assistantContentBuilder.ToString());

            var functionCalls = functionCallContentBuilder.Build();
            if (functionCalls.Count == 0) break;

            var functionCallContent = new ChatMessageContent(authorRole ?? default, content: null);
            chatHistory.Add(functionCallContent);

            foreach (var functionCall in functionCalls)
            {
                functionCallContent.Items.Add(functionCall);

                try
                {
                    var resultContent = await functionCall.InvokeAsync(kernel);
                    chatHistory.Add(resultContent.ToChatMessage());
                }
                catch (Exception ex)
                {
                    chatHistory.Add(new FunctionResultContent(functionCall, ex.Message).ToChatMessage());
                }
            }
        }

        var assistantOutput = chatHistory.Reverse().FirstOrDefault(c => c.Role == AuthorRole.Assistant)?.Content;
        if (assistantOutput is null)
        {
            Console.WriteLine("No output");
            return;
        }

        var yamlCodeBlock = YamlCodeBlockRegex().Match(assistantOutput);
        var yaml = yamlCodeBlock.Success ? yamlCodeBlock.Groups[1].Value : assistantOutput;

    }

    [GeneratedRegex(@"```\s*yaml([\s\S]+)```", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex YamlCodeBlockRegex();
}

public sealed class ConsoleLoggerFactory : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => new ConsoleLogger(categoryName);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    private class ConsoleLogger(string categoryName) : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{categoryName}] {formatter(state, exception)}");
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}

public sealed class WorkflowTools(IChatCompletionService chatCompletion)
{
    [KernelFunction("search_node")]
    [Description("Search for third-party nodes, useful when built-in nodes are not enough")]
    public async Task<string> SearchNodeAsync(
        [Description("Keywords or concise description in English")] string query)
    {
        var chatHistory = new ChatHistory();
        // chatHistory.AddSystemMessage(Prompts.FunctionCreator);
        chatHistory.AddUserMessage(query);
        var result = await chatCompletion.GetChatMessageContentAsync(chatHistory);
        return result.Content ?? "Not found";
    }

    [KernelFunction("create_function")]
    [Description("Enable an agent to create a custom function when built-in nodes are insufficient and third-party nodes are unavailable")]
    public async Task<string> CreateFunctionAsync(
        [Description("Detailed specification of the function to be created, including requirements, inputs, outputs, and expected behavior")]
        string description)
    {
        var chatHistory = new ChatHistory();
        // chatHistory.AddSystemMessage(Prompts.FunctionCreator);
        chatHistory.AddUserMessage(description);
        var result = await chatCompletion.GetChatMessageContentAsync(chatHistory);
        return result.Content ?? "Error occurred while creating the function";
    }
}