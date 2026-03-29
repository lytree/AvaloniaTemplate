using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Avalonia.Plugin.Shared.Converters;

public class ViewLocator: IDataTemplate
{
    private static readonly List<Func<object, Control?>> _viewResolvers = new();
    private static readonly ConcurrentDictionary<string, Type> _typeCache = new();
    private static readonly ConcurrentDictionary<Assembly, Dictionary<string, Type>> _assemblyTypeCache = new();

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
    }

    private static Type? GetTypeFromAssembly(string typeName, Assembly assembly)
    {
        // 检查类型缓存
        if (_typeCache.TryGetValue(typeName, out var cachedType))
        {
            return cachedType;
        }

        try
        {
            // 直接从指定程序集中查找类型
            var type = assembly.GetType(typeName);
            if (type != null)
            {
                _typeCache.TryAdd(typeName, type);
                return type;
            }

            // 尝试不同的大小写组合
            type = FindTypeIgnoreCase(assembly, typeName);
            if (type != null)
            {
                _typeCache.TryAdd(typeName, type);
                return type;
            }

            // 尝试从所有加载的程序集中查找
            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 跳过当前程序集，已经检查过了
                if (loadedAssembly == assembly)
                    continue;

                type = GetTypeFromSpecificAssembly(typeName, loadedAssembly);
                if (type != null)
                {
                    _typeCache.TryAdd(typeName, type);
                    return type;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Type? GetTypeFromSpecificAssembly(string typeName, Assembly assembly)
    {
        // 首先尝试直接查找
        var type = assembly.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        // 尝试大小写不敏感查找
        return FindTypeIgnoreCase(assembly, typeName);
    }

    private static Type? FindTypeIgnoreCase(Assembly assembly, string typeName)
    {
        // 获取程序集的类型缓存
        var assemblyTypes = _assemblyTypeCache.GetOrAdd(assembly, ass =>
        {
            var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var type in ass.GetTypes())
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

        // 在缓存中查找
        if (assemblyTypes.TryGetValue(typeName, out var type))
        {
            return type;
        }

        return null;
    }

    public Control? Build(object? param)
    {
        if (param is null) return null;

        // 1. 尝试使用插件注册的自定义解析器
        foreach (var resolver in _viewResolvers)
        {
            try
            {
                var control = resolver(param);
                if (control != null)
                {
                    return control;
                }
            }
            catch (Exception ex)
            {
                return new TextBlock { Text = $"Error in custom resolver: {ex.Message}" };
            }
        }

        var name = param.GetType().Name.Replace("ViewModel", "");
        
        // 2. 尝试从ViewModel所在的命名空间查找
        var viewModelType = param.GetType();
        var viewModelNamespace = viewModelType.Namespace;
        if (!string.IsNullOrEmpty(viewModelNamespace))
        {
            // 将ViewModel命名空间中的ViewModels替换为Pages
            var viewNamespace = viewModelNamespace.Replace("ViewModels", "Pages");
            var type = GetTypeFromAssembly($"{viewNamespace}.{name}", viewModelType.Assembly);
            if (type != null)
            {
                try
                {
                    return (Control)Activator.CreateInstance(type)!;
                }
                catch (Exception ex)
                {
                    return new TextBlock { Text = $"Error creating {name}: {ex.Message}" };
                }
            }

            // 4. 尝试其他常见的命名空间映射
            var alternativeMappings = new List<string>
            {
                viewModelNamespace.Replace(".ViewModels", ".Pages"),
                viewModelNamespace.Replace("ViewModels", "Pages")
            };

            foreach (var altNamespace in alternativeMappings)
            {
                type = GetTypeFromAssembly($"{altNamespace}.{name}", viewModelType.Assembly);
                if (type != null)
                {
                    try
                    {
                        return (Control)Activator.CreateInstance(type)!;
                    }
                    catch (Exception ex)
                    {
                        return new TextBlock { Text = $"Error creating {name}: {ex.Message}" };
                    }
                }
            }
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return true;
    }
}
