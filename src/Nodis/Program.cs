using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Backend.Services;
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
        // await WorkflowAgent.RunAsync();

        ServiceLocator.Build(x => x
            .AddHttpClient()
            .AddSingleton<YamlSerializerOptions>(_ => new YamlSerializerOptions
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
            .AddSingleton<IEnvironmentManager, LocalEnvironmentManager>()
            .UseAvaloniaApp());
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}