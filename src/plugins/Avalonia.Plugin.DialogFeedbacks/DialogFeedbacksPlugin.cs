using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.DialogFeedbacks.ViewModels;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.DialogFeedbacks;

public class DialogFeedbacksPlugin : IPlugin
{
    public string Name => "Dialog & Feedbacks Plugin";
    public string Version => "1.0.0";

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>
        {
            { MenuKeys.MenuKeyDialog, () => new DialogDemoViewModel() },
            { MenuKeys.MenuKeyDrawer, () => new DrawerDemoViewModel() },
            { MenuKeys.MenuKeyLoading, () => new LoadingDemoViewModel() },
            { MenuKeys.MenuKeyMessageBox, () => new MessageBoxDemoViewModel() },
            { MenuKeys.MenuKeyNotification, () => new NotificationDemoViewModel() },
            { MenuKeys.MenuKeyPopConfirm, () => new PopConfirmDemoViewModel() },
            { MenuKeys.MenuKeyToast, () => new ToastDemoViewModel() },
            { MenuKeys.MenuKeySkeleton, () => new SkeletonDemoViewModel() },
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var dialogAndFeedbacks = new MenuItemViewModel
        {
            MenuHeader = "Dialog & Feedbacks",
            Children = new()
            {
                new() { MenuHeader = "Dialog", Key = MenuKeys.MenuKeyDialog },
                new() { MenuHeader = "Drawer", Key = MenuKeys.MenuKeyDrawer },
                new() { MenuHeader = "Loading", Key = MenuKeys.MenuKeyLoading },
                new() { MenuHeader = "Message Box", Key = MenuKeys.MenuKeyMessageBox },
                new() { MenuHeader = "Notification", Key = MenuKeys.MenuKeyNotification },
                new() { MenuHeader = "PopConfirm", Key = MenuKeys.MenuKeyPopConfirm },
                new() { MenuHeader = "Toast", Key = MenuKeys.MenuKeyToast },
                new() { MenuHeader = "Skeleton", Key = MenuKeys.MenuKeySkeleton },
            }
        };
        menuItems.Add((null, dialogAndFeedbacks));

        return menuItems;
    }
}



