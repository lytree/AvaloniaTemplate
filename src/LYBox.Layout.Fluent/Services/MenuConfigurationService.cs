using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.Shared.ViewModels;

namespace LYBox.Layout.Fluent.Services;

/// <summary>
/// Fluent 布局的菜单配置服务实现。
/// 不预置任何默认菜单项（Fluent 有自己的内置页面导航），仅管理插件注册的菜单项。
/// </summary>
public sealed class MenuConfigurationService : IMenuConfigurationService
{
    private readonly ObservableCollection<MenuItemViewModel> _menuItems = [];
    private readonly ConcurrentDictionary<string, MenuItemViewModel> _menuItemsMap = new();

    private void BuildMenuItemsMap(IEnumerable<MenuItemViewModel> menuItems)
    {
        foreach (var menuItem in menuItems)
        {
            if (!string.IsNullOrEmpty(menuItem.Key))
            {
                _menuItemsMap[menuItem.Key] = menuItem;
            }

            if (menuItem.Children is { Count: > 0 })
            {
                BuildMenuItemsMap(menuItem.Children);
            }
        }
    }

    public ObservableCollection<MenuItemViewModel> GetMenuStructure()
    {
        return _menuItems;
    }

    public void RegisterMenuItem(MenuItemViewModel menuItem, string? parentKey = null)
    {
        if (parentKey == null)
        {
            _menuItems.Add(menuItem);
        }
        else
        {
            var parent = FindMenuItem(parentKey);
            if (parent != null)
            {
                parent.Children ??= new();
                parent.Children.Add(menuItem);
            }
            if (!string.IsNullOrEmpty(menuItem.Key))
            {
                _menuItemsMap[menuItem.Key] = menuItem;
            }
        }
    }

    private MenuItemViewModel? FindMenuItem(string key)
    {
        if (_menuItemsMap.TryGetValue(key, out var mappedItem))
            return mappedItem;
        return FindMenuItemRecursive(_menuItems, key);
    }

    private static MenuItemViewModel? FindMenuItemRecursive(IEnumerable<MenuItemViewModel> menuItems, string key)
    {
        foreach (var menuItem in menuItems)
        {
            if (menuItem.Key == key)
                return menuItem;

            if (menuItem.Children != null)
            {
                var foundItem = FindMenuItemRecursive(menuItem.Children, key);
                if (foundItem != null)
                    return foundItem;
            }
        }
        return null;
    }

    public void RegisterMenuItems(List<KeyValuePair<string?, MenuItemViewModel>> menuItems)
    {
        foreach (var (parentKey, menuItem) in menuItems)
        {
            RegisterMenuItem(menuItem, parentKey);
        }
    }

    public void RemoveMenuItem(string key)
    {
        _menuItemsMap.TryRemove(key, out _);
        var menuItemToRemove = FindMenuItem(key);
        _menuItems.Remove(menuItemToRemove);
    }

    public IEnumerable<string> GetMenuItemKeys()
    {
        return _menuItemsMap.Keys;
    }

    public MenuItemViewModel? GetMenuItemByKey(string key)
    {
        _menuItemsMap.TryGetValue(key, out var item);
        return item;
    }
}
