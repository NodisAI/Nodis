using Microsoft.Extensions.DependencyInjection.Extensions;
using Nodis.Backend.Services;
using Nodis.Core;

namespace Nodis.Backend;

public static class Application
{
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSignalR();

        var app = builder.Build();

        app.MapHub<HostObjectSynchronizationHub>("/ObjectSynchronization");

        await app.RunAsync();
    }
}