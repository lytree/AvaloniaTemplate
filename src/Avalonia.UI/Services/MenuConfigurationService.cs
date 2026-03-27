using System.Collections.Generic;
using System.Linq;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.ViewModels;
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
                _menuItemsMap[menuItem.Key] = new MenuItemViewModel
                {
                    MenuHeader = menuItem.MenuHeader,
                    Key = menuItem.Key,
                    Status = menuItem.Status,
                    IsSeparator = menuItem.IsSeparator,
                    Children = menuItem.Children != null ? new System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel>(
                        menuItem.Children.Select(child => new MenuItemViewModel
                        {
                            MenuHeader = child.MenuHeader,
                            Key = child.Key,
                            Status = child.Status,
                            IsSeparator = child.IsSeparator,
                            Children = child.Children != null ? new System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel>(
                                child.Children.Select(c => new MenuItemViewModel
                                {
                                    MenuHeader = c.MenuHeader,
                                    Key = c.Key,
                                    Status = c.Status,
                                    IsSeparator = c.IsSeparator,
                                    Children = null
                                })
                            ) : null
                        })
                    ) : null
                };
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

        var avaloniaMenuItem = new MenuItemViewModel
        {
            MenuHeader = menuItem.MenuHeader,
            Key = menuItem.Key,
            Status = menuItem.Status,
            IsSeparator = menuItem.IsSeparator,
            Children = menuItem.Children != null ? new System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel>(
                menuItem.Children.Select(child => new MenuItemViewModel
                {
                    MenuHeader = child.MenuHeader,
                    Key = child.Key,
                    Status = child.Status,
                    IsSeparator = child.IsSeparator,
                    Children = child.Children != null ? new System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel>(
                        child.Children.Select(c => new MenuItemViewModel
                        {
                            MenuHeader = c.MenuHeader,
                            Key = c.Key,
                            Status = c.Status,
                            IsSeparator = c.IsSeparator,
                            Children = null
                        })
                    ) : null
                })
            ) : null
        };

        if (parentKey == null)
        {
            // 添加到根菜单
            _menuViewModel.MenuItems.Add(avaloniaMenuItem);
        }
        else if (_menuItemsMap.TryGetValue(parentKey, out var parentMenuItem))
        {
            // 查找对应的 Avalonia 菜单项
            var avaloniaParentMenuItem = FindAvaloniaMenuItem(_menuViewModel.MenuItems, parentKey);
            if (avaloniaParentMenuItem != null)
            {
                // 添加到指定父菜单
                if (avaloniaParentMenuItem.Children == null)
                {
                    avaloniaParentMenuItem.Children = new();
                }
                avaloniaParentMenuItem.Children.Add(avaloniaMenuItem);
            }
        }

        // 更新菜单映射
        _menuItemsMap[menuItem.Key] = menuItem;
        if (menuItem.Children != null && menuItem.Children.Any())
        {
            foreach (var childItem in menuItem.Children)
            {
                _menuItemsMap[childItem.Key] = childItem;
            }
        }
    }

    private MenuItemViewModel FindAvaloniaMenuItem(IEnumerable<MenuItemViewModel> menuItems, string key)
    {
        foreach (var menuItem in menuItems)
        {
            if (menuItem.Key == key)
            {
                return menuItem;
            }

            if (menuItem.Children != null)
            {
                var foundItem = FindAvaloniaMenuItem(menuItem.Children, key);
                if (foundItem != null)
                {
                    return foundItem;
                }
            }
        }

        return null;
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
            RemoveMenuItemFromParent(key);
            _menuItemsMap.Remove(key);
        }
    }

    private void RemoveMenuItemFromParent(string key)
    {
        // 从根菜单查找
        var menuItemToRemove = FindAvaloniaMenuItem(_menuViewModel.MenuItems, key);
        if (menuItemToRemove != null && _menuViewModel.MenuItems.Remove(menuItemToRemove))
        {
            return;
        }

        // 从子菜单查找
        foreach (var parentItem in _menuViewModel.MenuItems)
        {
            if (RemoveFromChildren(parentItem, key))
            {
                return;
            }
        }
    }

    private bool RemoveFromChildren(MenuItemViewModel parentItem, string key)
    {
        var menuItemToRemove = FindAvaloniaMenuItem(parentItem.Children, key);
        if (parentItem.Children != null && menuItemToRemove != null && parentItem.Children.Remove(menuItemToRemove))
        {
            return true;
        }

        if (parentItem.Children != null)
        {
            foreach (var childItem in parentItem.Children)
            {
                if (RemoveFromChildren(childItem, key))
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

public static class EnumerableExtensions
{
    public static System.Collections.ObjectModel.ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source)
    {
        return new System.Collections.ObjectModel.ObservableCollection<T>(source);
    }
}