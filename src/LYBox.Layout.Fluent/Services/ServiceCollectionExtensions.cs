using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LYBox.Layout.Fluent.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Fluent 宿主层特有的 DI 服务：导航、菜单配置。
    /// 这些实现不依赖 Ursa 的 ViewModel/Page/Theme 类型，与 Ursa 布局完全独立。
    /// 调用方应先调用 LYBox.Layout.Core.Services.ServiceCollectionExtensions.AddAvaloniaServices()
    /// 注册核心服务，再调用本方法补充 Fluent 层服务。
    /// </summary>
    public static IServiceCollection AddFluentServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();
        return services;
    }
}
