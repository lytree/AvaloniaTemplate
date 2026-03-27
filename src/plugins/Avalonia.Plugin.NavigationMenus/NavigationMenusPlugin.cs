using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.NavigationMenus.ViewModels;

namespace Avalonia.Plugin.NavigationMenus;

public class NavigationMenusPlugin : IPlugin
{
    public string Name => "Navigation & Menus Plugin";
    public string Version => "1.0.0";

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>
        {
            { MenuKeys.MenuKeyAnchor, () => new AnchorDemoViewModel() },
            { MenuKeys.MenuKeyBreadcrumb, () => new BreadcrumbDemoViewModel() },
            { MenuKeys.MenuKeyNavMenu, () => new NavMenuDemoViewModel() },
            { MenuKeys.MenuKeyPagination, () => new PaginationDemoViewModel() },
            { MenuKeys.MenuKeyToolBar, () => new ToolBarDemoViewModel() },
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var navigationAndMenus = new MenuItemViewModel
        {
            MenuHeader = "Navigation & Menus",
            Children = new()
            {
                new() { MenuHeader = "Anchor", Key = MenuKeys.MenuKeyAnchor },
                new() { MenuHeader = "Breadcrumb", Key = MenuKeys.MenuKeyBreadcrumb },
                new() { MenuHeader = "Nav Menu", Key = MenuKeys.MenuKeyNavMenu, Status = "Updated" },
                new() { MenuHeader = "Pagination", Key = MenuKeys.MenuKeyPagination },
                new() { MenuHeader = "ToolBar", Key = MenuKeys.MenuKeyToolBar },
            }
        };
        menuItems.Add(("Controls", navigationAndMenus));

        return menuItems;
    }
}



