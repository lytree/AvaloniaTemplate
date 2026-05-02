using Avalonia.Plugin.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.UI.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();

        services.AddSingleton<PluginLoader>();
        services.AddSingleton<IPluginLoader>(sp => sp.GetRequiredService<PluginLoader>());
        services.AddSingleton<IPluginInstallationManager, PluginInstallationManager>();

        return services;
    }
}
