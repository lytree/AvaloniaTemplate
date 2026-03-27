using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.UI.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaServices(this IServiceCollection services)
    {
        // 注册导航服务
        services.AddSingleton<INavigationService, NavigationService>();
        
        // 注册菜单配置服务
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();

        // 可以在这里注册其他服务
        // services.AddSingleton<ISomeService, SomeService>();

        return services;
    }
}
