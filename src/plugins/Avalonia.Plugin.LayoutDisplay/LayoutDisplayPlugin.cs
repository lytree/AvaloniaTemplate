using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.LayoutDisplay.ViewModels;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.LayoutDisplay;

public class LayoutDisplayPlugin : IPlugin
{
    public string Name => "Layout & Display Plugin";
    public string Version => "1.0.0";

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>
        {
            { MenuKeys.MenuKeyAspectRatioLayout, () => new AspectRatioLayoutDemoViewModel() },
            { MenuKeys.MenuKeyAvatar, () => new AvatarDemoViewModel() },
            { MenuKeys.MenuKeyBadge, () => new BadgeDemoViewModel() },
            { MenuKeys.MenuKeyBanner, () => new BannerDemoViewModel() },
            { MenuKeys.MenuKeyDescriptions, () => new DescriptionsDemoViewModel() },
            { MenuKeys.MenuKeyDisableContainer, () => new DisableContainerDemoViewModel() },
            { MenuKeys.MenuKeyDivider, () => new DividerDemoViewModel() },
            { MenuKeys.MenuKeyDualBadge, () => new DualBadgeDemoViewModel() },
            { MenuKeys.MenuKeyImageViewer, () => new ImageViewerDemoViewModel() },
            { MenuKeys.MenuKeyElasticWrapPanel, () => new ElasticWrapPanelDemoViewModel() },
            { MenuKeys.MenuKeyMarquee, () => new MarqueeDemoViewModel() },
            { MenuKeys.MenuKeyNumberDisplayer, () => new NumberDisplayerDemoViewModel() },
            { MenuKeys.MenuKeyQrCode, () => new QrCodeDemoViewModel() },
            { MenuKeys.MenuKeyScrollToButton, () => new ScrollToButtonDemoViewModel() },
            { MenuKeys.MenuKeyTimeline, () => new TimelineDemoViewModel() },
            { MenuKeys.MenuKeyTwoTonePathIcon, () => new TwoTonePathIconDemoViewModel() }
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var layoutAndDisplay = new MenuItemViewModel
        {
            MenuHeader = "Layout & Display",
            Children = new()
            {
                new() { MenuHeader = "AspectRatioLayout", Key = MenuKeys.MenuKeyAspectRatioLayout },
                new() { MenuHeader = "Avatar", Key = MenuKeys.MenuKeyAvatar, Status = "WIP" },
                new() { MenuHeader = "Badge", Key = MenuKeys.MenuKeyBadge },
                new() { MenuHeader = "Banner", Key = MenuKeys.MenuKeyBanner },
                new() { MenuHeader = "Descriptions", Key = MenuKeys.MenuKeyDescriptions, Status = "New" },
                new() { MenuHeader = "Disable Container", Key = MenuKeys.MenuKeyDisableContainer },
                new() { MenuHeader = "Divider", Key = MenuKeys.MenuKeyDivider },
                new() { MenuHeader = "DualBadge", Key = MenuKeys.MenuKeyDualBadge },
                new() { MenuHeader = "ImageViewer", Key = MenuKeys.MenuKeyImageViewer },
                new() { MenuHeader = "ElasticWrapPanel", Key = MenuKeys.MenuKeyElasticWrapPanel },
                new() { MenuHeader = "Marquee", Key = MenuKeys.MenuKeyMarquee },
                new() { MenuHeader = "Number Displayer", Key = MenuKeys.MenuKeyNumberDisplayer, Status = "Updated" },
                new() { MenuHeader = "Qr Code", Key = MenuKeys.MenuKeyQrCode, Status = "New" },
                new() { MenuHeader = "Scroll To", Key = MenuKeys.MenuKeyScrollToButton },
                new() { MenuHeader = "Timeline", Key = MenuKeys.MenuKeyTimeline },
                new() { MenuHeader = "TwoTonePathIcon", Key = MenuKeys.MenuKeyTwoTonePathIcon }
            }
        };
        menuItems.Add((null, layoutAndDisplay));

        return menuItems;
    }

    public System.Reflection.Assembly GetAssembly()
    {
        return GetType().Assembly;
    }
}



