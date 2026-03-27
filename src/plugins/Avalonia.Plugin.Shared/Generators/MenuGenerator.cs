using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.Shared.Generators;

/// <summary>
/// 菜单项生成器，用于从ViewModel类型自动生成菜单项
/// </summary>
public static class MenuGenerator
{
    /// <summary>
    /// 从指定程序集中扫描带有MenuAttribute特性的ViewModel类型，并生成菜单项
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    /// <returns>生成的菜单项</returns>
    public static IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GenerateMenuItems(Assembly assembly)
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();
        
        // 扫描程序集中所有带有MenuAttribute特性的类型
        var viewModelTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<MenuAttribute>() != null);
        
        foreach (var type in viewModelTypes)
        {
            var menuAttribute = type.GetCustomAttribute<MenuAttribute>();
            if (menuAttribute != null)
            {
                var menuItem = new MenuItemViewModel
                {
                    MenuHeader = menuAttribute.Header,
                    Key = menuAttribute.Key,
                    Status = menuAttribute.Status,
                    Order = menuAttribute.Order
                };
                
                menuItems.Add((menuAttribute.ParentKey, menuItem));
            }
        }
        
        // 按顺序排序
        menuItems = menuItems.OrderBy(item => item.Item2.Order).ToList();
        
        return menuItems;
    }
    
    /// <summary>
    /// 从指定类型列表中扫描带有MenuAttribute特性的ViewModel类型，并生成菜单项
    /// </summary>
    /// <param name="types">要扫描的类型列表</param>
    /// <returns>生成的菜单项</returns>
    public static IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GenerateMenuItems(IEnumerable<Type> types)
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();
        
        foreach (var type in types)
        {
            var menuAttribute = type.GetCustomAttribute<MenuAttribute>();
            if (menuAttribute != null)
            {
                var menuItem = new MenuItemViewModel
                {
                    MenuHeader = menuAttribute.Header,
                    Key = menuAttribute.Key,
                    Status = menuAttribute.Status,
                    Order = menuAttribute.Order
                };
                
                menuItems.Add((menuAttribute.ParentKey, menuItem));
            }
        }
        
        // 按顺序排序
        menuItems = menuItems.OrderBy(item => item.Item2.Order).ToList();
        
        return menuItems;
    }
    
    /// <summary>
    /// 获取由代码生成器生成的菜单项
    /// </summary>
    /// <returns>生成的菜单项</returns>
    public static IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetGeneratedMenuItems()
    {
        try
        {
            // 尝试通过反射获取GeneratedMenuItems类型
            var generatedMenuItemsType = Type.GetType("Avalonia.Plugin.Shared.Generators.GeneratedMenuItems");
            if (generatedMenuItemsType != null)
            {
                // 尝试获取GetGeneratedMenuItems方法
                var method = generatedMenuItemsType.GetMethod("GetGeneratedMenuItems");
                if (method != null)
                {
                    // 调用方法并获取结果
                    var result = method.Invoke(null, null);
                    if (result != null)
                    {
                        // 转换结果为正确的类型
                        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();
                        var enumerable = result as System.Collections.IEnumerable;
                        if (enumerable != null)
                        {
                            foreach (var item in enumerable)
                            {
                                // 检查item是否是正确的元组类型
                                var itemType = item.GetType();
                                if (itemType.IsValueType && itemType.IsGenericType)
                                {
                                    var parentKey = itemType.GetProperty("ParentKey")?.GetValue(item) as string;
                                    var menuItem = itemType.GetProperty("MenuItem")?.GetValue(item) as MenuItemViewModel;
                                    if (menuItem != null)
                                    {
                                        menuItems.Add((parentKey, menuItem));
                                    }
                                }
                            }
                        }
                        return menuItems;
                    }
                }
            }
        }
        catch (Exception)
        {
            // 如果代码生成器还没有生成代码，返回空列表
        }
        
        return Enumerable.Empty<(string? ParentKey, MenuItemViewModel MenuItem)>();
    }
}
