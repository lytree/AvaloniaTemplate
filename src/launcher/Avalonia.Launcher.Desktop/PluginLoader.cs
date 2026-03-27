using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Desktop;

public class PluginLoader
{
    private readonly List<IPlugin> _plugins = new();

    /// <summary>
    /// 加载插件
    /// </summary>
    /// <param name="pluginsDirectory">插件目录</param>
    public void LoadPlugins(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            Directory.CreateDirectory(pluginsDirectory);
            return;
        }

        var dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll");
        foreach (var dllFile in dllFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllFile);
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        var plugin = (IPlugin)Activator.CreateInstance(type)!;
                        _plugins.Add(plugin);
                        plugin.Initialize();
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录错误信息
                Console.WriteLine($"Error loading plugin {dllFile}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 获取所有插件
    /// </summary>
    /// <returns>插件列表</returns>
    public IEnumerable<IPlugin> GetPlugins()
    {
        return _plugins;
    }

    /// <summary>
    /// 获取所有插件提供的导航项
    /// </summary>
    /// <returns>导航项字典</returns>
    public Dictionary<string, Avalonia.Plugin.Shared.ViewModelFactory> GetAllNavigationItems()
    {
        var navigationItems = new Dictionary<string, Avalonia.Plugin.Shared.ViewModelFactory>();
        foreach (var plugin in _plugins)
        {
            var items = plugin.GetNavigationItems();
            foreach (var (key, factory) in items)
            {
                navigationItems[key] = factory;
            }
        }
        return navigationItems;
    }

    /// <summary>
    /// 获取所有插件提供的菜单项
    /// </summary>
    /// <returns>菜单项列表</returns>
    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetAllMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();
        foreach (var plugin in _plugins)
        {
            var items = plugin.GetMenuItems();
            menuItems.AddRange(items);
        }
        return menuItems;
    }
}