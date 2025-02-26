using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Extensions;
using Nodis.Interfaces;
using Nodis.Services;
using Nodis.ViewModels;
using Nodis.Views;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Nodis;

public class App : Application, IKeyedServiceProvider
{
    public static App Singleton => Current.NotNull<App>("App is not initialized.");

    public ServiceCollection ServiceCollection { get; } = [];
    private ServiceProvider? serviceProvider;

    public override void Initialize()
    {
        #region BasicServices

        ServiceCollection
            .AddSingleton<HttpClient>()
            .AddSingleton<IBashExecutor>(Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => new MinGWBashExecutor(),
                _ => throw new PlatformNotSupportedException()
            });

        #endregion

        ServiceCollection
            .AddSingleton<ISukiDialogManager, SukiDialogManager>()
            .AddSingleton<ISukiToastManager, SukiToastManager>();

        #region MainWindowPages

        ServiceCollection
            .AddSingleton<NodeStorePageViewModel>()
            .AddSingleton<IMainWindowPage, NodeStorePage>();

        #endregion

        ServiceCollection
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<MainWindow>();

        serviceProvider = ServiceCollection.BuildServiceProvider();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = Resolve<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    #region ServiceProvider

    public object? GetService(Type serviceType)
    {
        if (serviceProvider == null) throw new InvalidOperationException("ServiceProvider is not initialized.");
        return serviceProvider.GetService(serviceType);
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        if (serviceProvider == null) throw new InvalidOperationException("ServiceProvider is not initialized.");
        return serviceProvider.GetKeyedService(serviceType, serviceKey);
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        if (serviceProvider == null) throw new InvalidOperationException("ServiceProvider is not initialized.");
        return serviceProvider.GetRequiredKeyedService(serviceType, serviceKey);
    }

    public static T Resolve<T>(object? key = null) => (T)Singleton.GetRequiredKeyedService(typeof(T), key);

    #endregion
}