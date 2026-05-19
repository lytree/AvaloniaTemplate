using System.Collections.Concurrent;

namespace Avalonia.Plugin.Shared;

public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;
    private static readonly ConcurrentDictionary<Type, object> _registeredServices = new();

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static IServiceProvider GetServiceProvider()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator has not been initialized. Call Initialize() first.");
        }
        return _serviceProvider;
    }

    public static void RegisterService<T>(T service) where T : class
    {
        _registeredServices[typeof(T)] = service;
    }

    public static T GetService<T>() where T : class
    {
        if (_registeredServices.TryGetValue(typeof(T), out var registered))
        {
            return (T)registered;
        }

        var service = GetServiceProvider().GetService(typeof(T)) as T;
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not found in the service provider.");
        }
        return service;
    }

    public static bool TryGetService<T>(out T? service) where T : class
    {
        if (_registeredServices.TryGetValue(typeof(T), out var registered))
        {
            service = (T)registered;
            return true;
        }

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
