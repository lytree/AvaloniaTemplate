using System.Collections.Generic;
using System.Linq;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public class MenuConfigurationService : IMenuConfigurationService
{
    private readonly MenuViewModel _menuViewModel;
    private readonly Dictionary<string, MenuItemViewModel> _menuItemsMap = new();

    public MenuConfigurationService()
    {
        _menuViewModel = new MenuViewModel();
        BuildMenuItemsMap(_menuViewModel.MenuItems);
    }

    private void BuildMenuItemsMap(IEnumerable<MenuItemViewModel> menuItems)
    {
        foreach (var menuItem in menuItems)
        {
            if (!string.IsNullOrEmpty(menuItem.Key))
            {
                _menuItemsMap[menuItem.Key] = menuItem;
            }

            if (menuItem.Children != null && menuItem.Children.Any())
            {
                BuildMenuItemsMap(menuItem.Children);
            }
        }
    }

    public MenuViewModel GetMenuStructure()
    {
        return _menuViewModel;
    }

    public void RegisterMenuItem(MenuItemViewModel menuItem, string? parentKey = null)
    {
        if (string.IsNullOrEmpty(menuItem.Key))
        {
            return;
        }

        if (parentKey == null)
        {
            // 添加到根菜单
            _menuViewModel.MenuItems.Add(menuItem);
        }
        else if (_menuItemsMap.TryGetValue(parentKey, out var parentMenuItem))
        {
            // 添加到指定父菜单
            if (parentMenuItem.Children == null)
            {
                parentMenuItem.Children = new();
            }
            parentMenuItem.Children.Add(menuItem);
        }

        // 更新菜单映射
        _menuItemsMap[menuItem.Key] = menuItem;
        if (menuItem.Children != null && menuItem.Children.Any())
        {
            BuildMenuItemsMap(menuItem.Children);
        }
    }

    public void RegisterMenuItems(IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> menuItems)
    {
        foreach (var (parentKey, menuItem) in menuItems)
        {
            RegisterMenuItem(menuItem, parentKey);
        }
    }

    public void RemoveMenuItem(string key)
    {
        if (_menuItemsMap.TryGetValue(key, out var menuItem))
        {
            // 查找并移除菜单项
            RemoveMenuItemFromParent(menuItem);
            _menuItemsMap.Remove(key);
        }
    }

    private void RemoveMenuItemFromParent(MenuItemViewModel menuItem)
    {
        // 从根菜单查找
        if (_menuViewModel.MenuItems.Remove(menuItem))
        {
            return;
        }

        // 从子菜单查找
        foreach (var parentItem in _menuViewModel.MenuItems)
        {
            if (RemoveFromChildren(parentItem, menuItem))
            {
                return;
            }
        }
    }

    private bool RemoveFromChildren(MenuItemViewModel parentItem, MenuItemViewModel menuItem)
    {
        if (parentItem.Children != null && parentItem.Children.Remove(menuItem))
        {
            return true;
        }

        if (parentItem.Children != null)
        {
            foreach (var childItem in parentItem.Children)
            {
                if (RemoveFromChildren(childItem, menuItem))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IEnumerable<string> GetMenuItemKeys()
    {
        return _menuItemsMap.Keys;
    }
}