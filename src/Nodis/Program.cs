using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Nodis.Backend.Interfaces;
using Nodis.Backend.Services;
using Nodis.Core.Agent;
using Nodis.Core.Extensions;
using Nodis.Core.Interfaces;
using Nodis.Core.Models;
using Nodis.Core.Models.Workflow;
using Nodis.Frontend;
using VYaml.Serialization;

namespace Nodis;

internal static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        ServiceLocator.Build(
            x => x
                .AddHttpClient()
                .AddSingleton(
                    new JsonSerializerOptions
                    {
                        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        Converters =
                        {
                            new JsonStringEnumConverter()
                        },
                        AllowTrailingCommas = true
                    })
                .AddSingleton(
                    new YamlSerializerOptions
                    {
                        Resolver = CompositeResolver.Create(
                            [
                                new ChecksumYamlFormatter(),
                                new WorkflowNodePortConnectionYamlFormatter(),
                                new NameAndVersionConstraintsYamlFormatter()
                            ],
                            [
                                StandardResolver.Instance
                            ]
                        )
                    })
                .AddSingleton<INativeInterop>(
                    _ => new WindowsNativeInterop()
                )
                .AddSingleton<IKernelMemory>(
                    _ => new KernelMemoryBuilder()
                        .WithOpenAIDefaults(
                            Environment.GetEnvironmentVariable("NODIS_API_KEY", EnvironmentVariableTarget.User).NotNull("NODIS_API_KEY is not set"))
                        .Configure(builder => builder.Services.AddLogging(l => l.AddSimpleConsole()))
                        .Build<MemoryServerless>())
                .AddSingleton<IEnvironmentManager, LocalEnvironmentManager>()
                .UseAvaloniaApp());

        // await WorkflowAgent.RunAsync();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}