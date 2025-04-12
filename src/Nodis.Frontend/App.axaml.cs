using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using HotAvalonia;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Frontend.Interfaces;
using Nodis.Frontend.ViewModels;
using Nodis.Frontend.Views;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Nodis.Frontend;

public static class AvaloniaAppExtension
{
    public static IServiceCollection UseAvaloniaApp(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IObjectSynchronizationHub>(_ =>
            HubConnectionProxyFactory.CreateHubProxy<IObjectSynchronizationHub>());

        serviceCollection
            .AddSingleton<ISukiDialogManager, SukiDialogManager>()
            .AddSingleton<ISukiToastManager, SukiToastManager>();

        #region MainWindowPages

        serviceCollection
            .AddSingleton<WorkflowEditPageViewModel>()
            .AddSingleton<IMainWindowPage, WorkflowEditPage>()
            .AddSingleton<MarketplacePageViewModel>()
            .AddSingleton<IMainWindowPage, MarketplacePage>();

        #endregion

        serviceCollection
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<MainWindow>();

        return serviceCollection;
    }
}

public class App : Application
{
    public override void Initialize()
    {
        ImageLoader.AsyncImageLoader = new DiskCachedWebImageLoader(IEnvironmentManager.CacheFolderPath);
        this.EnableHotReload();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = ServiceLocator.Resolve<MainWindow>();
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
}