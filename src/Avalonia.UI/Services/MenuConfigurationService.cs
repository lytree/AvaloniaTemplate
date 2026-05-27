using System.Collections.Generic;
using System.Linq;
using Avalonia.Plugin.Shared.ViewModels;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public class MenuConfigurationService : IMenuConfigurationService
{
    private readonly MenuViewModel _menuViewModel;
    private readonly Dictionary<string, MenuItemViewModel> _menuItemsMap = new();
    private bool _mapBuilt;

    public MenuConfigurationService()
    {
        _menuViewModel = new MenuViewModel();
    }

    private static MenuItemViewModel DeepClone(MenuItemViewModel source)
    {
        var clone = new MenuItemViewModel
        {
            MenuHeader = source.RawHeader ?? source.MenuHeader,
            Key = source.Key,
            Status = source.Status,
            IsSeparator = source.IsSeparator,
        };
        if (source.Children is { Count: > 0 })
        {
            foreach (var child in source.Children)
            {
                clone.Children.Add(DeepClone(child));
            }
        }
        return clone;
    }

    private void BuildMenuItemsMap(IEnumerable<MenuItemViewModel> menuItems)
    {
        foreach (var menuItem in menuItems)
        {
            if (!string.IsNullOrEmpty(menuItem.Key))
            {
                _menuItemsMap[menuItem.Key] = DeepClone(menuItem);
            }

            if (menuItem.Children != null && menuItem.Children.Any())
            {
                BuildMenuItemsMap(menuItem.Children);
            }
        }
    }

    public MenuViewModel GetMenuStructure()
    {
        if (!_mapBuilt)
        {
            BuildMenuItemsMap(_menuViewModel.MenuItems);
            _mapBuilt = true;
        }
        return _menuViewModel;
    }

    public void RegisterMenuItem(MenuItemViewModel menuItem, string? parentKey = null)
    {
        if (parentKey == null)
        {
            _menuViewModel.MenuItems.Add(menuItem);
        }
        else
        {
            var avaloniaParentMenuItem = FindAvaloniaMenuItem(parentKey);
            if (avaloniaParentMenuItem != null)
            {
                if (avaloniaParentMenuItem.Children == null)
                {
                    avaloniaParentMenuItem.Children = new();
                }
                avaloniaParentMenuItem.Children.Add(menuItem);
            }
            _menuItemsMap[menuItem.Key] = menuItem;
        }
    }

    private MenuItemViewModel? FindAvaloniaMenuItem(string key)
    {
        if (_menuItemsMap.TryGetValue(key, out var mappedItem))
            return mappedItem;
        return FindAvaloniaMenuItemRecursive(_menuViewModel.MenuItems, key);
    }

    private static MenuItemViewModel? FindAvaloniaMenuItemRecursive(IEnumerable<MenuItemViewModel> menuItems, string key)
    {
        foreach (var menuItem in menuItems)
        {
            if (menuItem.Key == key)
                return menuItem;

            if (menuItem.Children != null)
            {
                var foundItem = FindAvaloniaMenuItemRecursive(menuItem.Children, key);
                if (foundItem != null)
                    return foundItem;
            }
        }
        return null;
    }

    public void RegisterMenuItems(List<KeyValuePair<string, MenuItemViewModel>> menuItems)
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
            RemoveMenuItemFromParent(key);
            _menuItemsMap.Remove(key);
        }
    }

    private void RemoveMenuItemFromParent(string key)
    {
        var menuItemToRemove = FindAvaloniaMenuItem(key);
        if (menuItemToRemove != null && _menuViewModel.MenuItems.Remove(menuItemToRemove))
        {
            return;
        }

        foreach (var parentItem in _menuViewModel.MenuItems)
        {
            if (RemoveFromChildren(parentItem, key))
                return;
        }
    }

    private static bool RemoveFromChildren(MenuItemViewModel parentItem, string key)
    {
        if (parentItem.Children == null) return false;

        var menuItemToRemove = FindAvaloniaMenuItemRecursive(parentItem.Children, key);
        if (menuItemToRemove != null && parentItem.Children.Remove(menuItemToRemove))
            return true;

        foreach (var childItem in parentItem.Children)
        {
            if (RemoveFromChildren(childItem, key))
                return true;
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