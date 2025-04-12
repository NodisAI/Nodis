using Microsoft.Extensions.DependencyInjection.Extensions;
using Nodis.Backend.Services;
using Nodis.Core;

namespace Nodis.Backend;

public static class Application
{
    public static async Task RunAsync(string[] args, IEnumerable<ServiceDescriptor> basicServices)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.Add(basicServices);
        builder.Services.AddSignalR();

        var app = builder.Build();
        ServiceLocator.Build(app.Services);

        app.MapHub<HostObjectSynchronizationHub>("/ObjectSynchronization");

        await app.RunAsync();
    }
}