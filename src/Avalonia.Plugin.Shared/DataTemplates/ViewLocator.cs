using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Avalonia.Plugin.Shared.Converters;

public class ViewLocator: IDataTemplate
{
    private static readonly List<Func<object, Control?>> _viewResolvers = new();
    private static readonly ConcurrentDictionary<string, Type?> _typeCache = new();
    private static readonly ConcurrentDictionary<Assembly, Dictionary<string, Type>> _assemblyTypeCache = new();
    private static readonly ConcurrentDictionary<Type, Func<Control>> _instanceCreators = new(); // 缓存实例创建方法
    private static readonly List<Func<string, string>> _namespaceMappings = new()
    {
        ns => ns.Replace("ViewModels", "Views"),
        ns => ns.Replace("ViewModels", "Pages"),
        ns => ns.Replace(".ViewModels", ".Views"),
        ns => ns.Replace(".ViewModels", ".Pages"),
        ns => ns + ".Views",
        ns => ns + ".Pages"
    };
    private static readonly List<Action<string>> _logHandlers = new();
    private static Func<Type, object>? _serviceProvider; // 依赖注入容器
    private const int MaxCacheSize = 1000;
    private static int _cacheSize = 0;
    
    // 性能统计
    private static long _totalBuildTime = 0;
    private static int _buildCount = 0;
    private static int _cacheHits = 0;
    private static int _cacheMisses = 0;

    public static void AddViewResolver(Func<object, Control?> resolver)
    {
        if (resolver != null)
        {
            _viewResolvers.Add(resolver);
        }
    }

    public static void ClearViewResolvers()
    {
        _viewResolvers.Clear();
    }

    public static void ClearCache()
    {
        _typeCache.Clear();
        _assemblyTypeCache.Clear();
        _instanceCreators.Clear();
        _cacheSize = 0;
        // 重置性能统计数据
        _totalBuildTime = 0;
        _buildCount = 0;
        _cacheHits = 0;
        _cacheMisses = 0;
    }

    public static string GetPerformanceReport()
    {
        var avgBuildTime = _buildCount > 0 ? (_totalBuildTime / (double)_buildCount).ToString("F2") : "0";
        var cacheHitRate = (_cacheHits + _cacheMisses) > 0 ? ((double)_cacheHits / (_cacheHits + _cacheMisses) * 100).ToString("F2") : "0";
        
        return $"ViewLocator Performance Report:\n" +
               $"Total Builds: {_buildCount}\n" +
               $"Average Build Time: {avgBuildTime} ms\n" +
               $"Cache Hits: {_cacheHits}\n" +
               $"Cache Misses: {_cacheMisses}\n" +
               $"Cache Hit Rate: {cacheHitRate}%\n" +
               $"Current Cache Size: {_cacheSize}/{MaxCacheSize}";
    }

    private static Func<Control> CreateInstanceCreator(Type type)
    {
        // 尝试获取无参构造函数
        var constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor != null)
        {
            // 创建一个委托来缓存构造函数调用
            return () => (Control)constructor.Invoke(null);
        }
        // 如果没有无参构造函数，回退到 Activator.CreateInstance
        return () => (Control)Activator.CreateInstance(type)!;
    }

    public static void AddNamespaceMapping(Func<string, string> mapping)
    {
        if (mapping != null)
        {
            _namespaceMappings.Add(mapping);
        }
    }

    public static void ClearNamespaceMappings()
    {
        _namespaceMappings.Clear();
        // 添加默认映射
        _namespaceMappings.Add(ns => ns.Replace("ViewModels", "Views"));
        _namespaceMappings.Add(ns => ns.Replace("ViewModels", "Pages"));
        _namespaceMappings.Add(ns => ns.Replace(".ViewModels", ".Views"));
        _namespaceMappings.Add(ns => ns.Replace(".ViewModels", ".Pages"));
        _namespaceMappings.Add(ns => ns + ".Views");
        _namespaceMappings.Add(ns => ns + ".Pages");
    }

    public static void AddLogHandler(Action<string> logHandler)
    {
        if (logHandler != null)
        {
            _logHandlers.Add(logHandler);
        }
    }

    public static void ClearLogHandlers()
    {
        _logHandlers.Clear();
    }

    public static void SetServiceProvider(Func<Type, object> serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static void Log(string message)
    {
        // 快速路径：如果没有日志处理程序，直接返回
        if (_logHandlers.Count == 0)
        {
            return;
        }
        
        foreach (var handler in _logHandlers)
        {
            try
            {
                handler(message);
            }
            catch
            {
                // 忽略日志处理程序的异常
            }
        }
    }

    private static Type? GetTypeFromAssembly(string typeName, Assembly assembly)
    {
        // 生成缓存键，包含程序集信息，避免不同程序集间的类型冲突
        var cacheKey = $"{assembly.FullName}:{typeName}";
        
        // 检查类型缓存 - 最快速的路径
        if (_typeCache.TryGetValue(cacheKey, out var cachedType))
        {
            _cacheHits++;
            return cachedType;
        }

        _cacheMisses++;

        // 快速路径：直接从指定程序集中查找类型
        var type = assembly.GetType(typeName);
        if (type != null)
        {
            AddToCache(cacheKey, type);
            return type;
        }

        // 较慢的路径：尝试不同的大小写组合
        type = FindTypeIgnoreCase(assembly, typeName);
        if (type != null)
        {
            AddToCache(cacheKey, type);
            return type;
        }

        // 最慢的路径：尝试从所有加载的程序集中查找
        foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // 跳过当前程序集，已经检查过了
            if (loadedAssembly == assembly)
                continue;

            type = GetTypeFromSpecificAssembly(typeName, loadedAssembly);
            if (type != null)
            {
                AddToCache(cacheKey, type);
                return type;
            }
        }

        // 缓存未找到的类型，避免重复查找
        AddToCache(cacheKey, null);
        return null;
    }

    private static void AddToCache(string key, Type? type)
    {
        // 检查缓存大小，如果超过限制，清理缓存
        if (_cacheSize >= MaxCacheSize)
        {
            ClearCache();
        }
        
        if (_typeCache.TryAdd(key, type))
        {
            _cacheSize++;
        }
    }

    private static Type? GetTypeFromSpecificAssembly(string typeName, Assembly assembly)
    {
        // 快速路径：直接查找类型
        var type = assembly.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        // 较慢路径：尝试大小写不敏感查找
        return FindTypeIgnoreCase(assembly, typeName);
    }

    private static Type? FindTypeIgnoreCase(Assembly assembly, string typeName)
    {
        // 获取程序集的类型缓存 - 使用 Lazy 加载，避免在首次访问时的性能开销
        var assemblyTypes = _assemblyTypeCache.GetOrAdd(assembly, ass =>
        {
            var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // 只在需要时才加载程序集的所有类型
                var allTypes = ass.GetTypes();
                foreach (var type in allTypes)
                {
                    if (!string.IsNullOrEmpty(type.FullName))
                    {
                        types[type.FullName] = type;
                    }
                }
            }
            catch
            {
                // 忽略无法加载类型的程序集
            }
            return types;
        });

        // 在缓存中查找 - 快速路径
        if (assemblyTypes.TryGetValue(typeName, out var type))
        {
            return type;
        }

        return null;
    }

    private static string GetViewName(string viewModelName)
    {
        // 处理泛型类型
        if (viewModelName.Contains('`'))
        {
            var genericPartIndex = viewModelName.IndexOf('`');
            var baseName = viewModelName.Substring(0, genericPartIndex);
            var genericPart = viewModelName.Substring(genericPartIndex);
            return baseName.Replace("ViewModel", "") + genericPart;
        }
        return viewModelName.Replace("ViewModel", "");
    }

    public Control? Build(object? param)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            if (param is null) return null;

            var viewModelType = param.GetType();
            var viewModelName = viewModelType.Name;
            var name = GetViewName(viewModelName);
            var viewModelNamespace = viewModelType.Namespace;

            // 快速路径：如果没有命名空间，直接返回未找到
            if (string.IsNullOrEmpty(viewModelNamespace))
            {
                Log($"ViewLocator: View not found for {viewModelName} (no namespace)");
                return new TextBlock { Text = $"View not found for {viewModelName} (no namespace)" };
            }

            Log($"ViewLocator: Looking for view for {viewModelName} in namespace {viewModelNamespace}");

            // 1. 尝试使用插件注册的自定义解析器
            foreach (var resolver in _viewResolvers)
            {
                try
                {
                    var control = resolver(param);
                    if (control != null)
                    {
                        Log($"ViewLocator: Found view via custom resolver for {viewModelName}");
                        return control;
                    }
                }
                catch (Exception ex)
                {
                    Log($"ViewLocator: Error in custom resolver for {viewModelName}: {ex.Message}");
                    return new TextBlock { Text = $"Error in custom resolver for {viewModelName}: {ex.Message}" };
                }
            }

            // 2. 尝试所有命名空间映射
            foreach (var mapping in _namespaceMappings)
            {
                try
                {
                    var viewNamespace = mapping(viewModelNamespace);
                    // 避免重复的字符串拼接，直接在 GetTypeFromAssembly 中使用
                    var type = GetTypeFromAssembly($"{viewNamespace}.{name}", viewModelType.Assembly);
                    if (type != null)
                        {
                            // 3. 创建视图实例
                            Control control;
                            if (_serviceProvider != null)
                            {
                                try
                                {
                                    control = (Control)_serviceProvider(type);
                                    Log($"ViewLocator: Successfully created view {viewNamespace}.{name} via DI");
                                }
                                catch (Exception ex)
                                {
                                    Log($"ViewLocator: Error creating {viewNamespace}.{name} via DI, falling back to cached creator: {ex.Message}");
                                    // 使用缓存的实例创建器
                                    var creator = _instanceCreators.GetOrAdd(type, CreateInstanceCreator);
                                    control = creator();
                                    Log($"ViewLocator: Successfully created view {viewNamespace}.{name} via cached creator");
                                }
                            }
                            else
                            {
                                // 使用缓存的实例创建器
                                var creator = _instanceCreators.GetOrAdd(type, CreateInstanceCreator);
                                control = creator();
                                Log($"ViewLocator: Successfully created view {viewNamespace}.{name} via cached creator");
                            }
                            return control;
                        }
                }
                catch (Exception ex)
                {
                    Log($"ViewLocator: Error mapping namespace for {viewModelName}: {ex.Message}");
                    // 忽略映射错误，继续尝试下一个
                }
            }
            
            Log($"ViewLocator: View not found for {viewModelName} (namespace: {viewModelNamespace})");
            return new TextBlock { Text = $"View not found for {viewModelName} (namespace: {viewModelNamespace})" };
        }
        finally
        {
            stopwatch.Stop();
            _totalBuildTime += stopwatch.ElapsedMilliseconds;
            _buildCount++;
        }
    }

    public bool Match(object? data)
    {
        return true;
    }
}
