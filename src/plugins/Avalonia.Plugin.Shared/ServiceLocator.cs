using System;

namespace Avalonia.Plugin.Shared;

/// <summary>
/// 服务定位器，用于获取依赖注入容器中的服务
/// </summary>
public static class ServiceLocator
{
    /// <summary>
    /// 依赖注入容器
    /// </summary>
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// 初始化服务定位器
    /// </summary>
    /// <param name="serviceProvider">依赖注入容器</param>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// 获取依赖注入容器
    /// </summary>
    /// <returns>依赖注入容器</returns>
    public static IServiceProvider GetServiceProvider()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");
        }
        return _serviceProvider;
    }

    /// <summary>
    /// 获取指定类型的服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例</returns>
    public static T GetService<T>() where T : class
    {
        var service = GetServiceProvider().GetService(typeof(T)) as T;
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not found in the service provider.");
        }
        return service;
    }

    /// <summary>
    /// 尝试获取指定类型的服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="service">服务实例</param>
    /// <returns>是否获取成功</returns>
    public static bool TryGetService<T>(out T? service) where T : class
    {
        try
        {
            service = GetServiceProvider().GetService(typeof(T)) as T;
            return service != null;
        }
        catch
        {
            service = null;
            return false;
        }
    }
}
