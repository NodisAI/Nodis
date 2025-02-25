using Antelcat.DependencyInjectionEx;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Nodis.Extensions;
using Nodis.ViewModels;
using Nodis.Views;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using ServiceProviderOptions = Antelcat.DependencyInjectionEx.ServiceProviderOptions;

namespace Nodis;

public class App : Application, IKeyedServiceProvider
{
    public static App Singleton => Current as App ?? throw new InvalidOperationException("App is not initialized.");

    public ServiceCollection ServiceCollection { get; } = [];
    private ServiceProviderEx? serviceProvider;

    public override void Initialize()
    {
        ServiceCollection
            .AddSingleton<ISukiDialogManager, SukiDialogManager>()
            .AddSingleton<ISukiToastManager, SukiToastManager>();

        ServiceCollection
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<MainWindow>();

        serviceProvider = ServiceCollection.BuildServiceProviderEx(new ServiceProviderOptions
        {
            CallbackTime = CallbackTime.Finally,
            ListenKind = ServiceResolveKind.Constructor
        });
        serviceProvider.ServiceResolved += HandleServiceResolved;

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

    private static void HandleServiceResolved(IServiceProvider serviceProvider, Type serviceType, object instance, ServiceResolveKind kind)
    {
        if (instance is not StyledElement styledElement) return;

        foreach (var interfaceType in instance.GetType().GetInterfaces().Where(i => i.IsGenericType))
        {
            ReactiveViewModelBase? viewModel = null;

            if (interfaceType.GetGenericTypeDefinition() == typeof(IReactiveViewWithServiceFactory<>))
            {
                viewModel = interfaceType
                    .GetProperty(nameof(IReactiveViewWithServiceFactory<ReactiveViewModelBase>.ServiceFactory))!
                    .GetValue(instance)
                    .NotNull<Delegate>()
                    .DynamicInvoke(serviceProvider)
                    .NotNull<ReactiveViewModelBase>(
                        $"Cannot resolve {nameof(ReactiveViewModelBase)} for IReactiveViewWithServiceFactory: {serviceType}");
            }
            else if (interfaceType.GetGenericTypeDefinition() == typeof(IReactiveView<>))
            {
                viewModel = serviceProvider
                    .GetRequiredService(interfaceType.GenericTypeArguments[0])
                    .NotNull<ReactiveViewModelBase>(
                        $"Cannot resolve {nameof(ReactiveViewModelBase)} for IReactiveView: {serviceType}");
            }
            if (viewModel == null) continue;

            viewModel.BindUnchecked(styledElement);
            break;
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