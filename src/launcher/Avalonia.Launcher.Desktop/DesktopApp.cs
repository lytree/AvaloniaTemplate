using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Plugin.ButtonsInputs;
using Avalonia.Plugin.DateTimeControls;
using Avalonia.Plugin.DialogFeedbacks;
using Avalonia.Plugin.LayoutDisplay;
using Avalonia.Plugin.NavigationMenus;
using Avalonia.Plugin.Shared;
using Avalonia.UI.Services;
using Avalonia.UI.ViewModels;
using Avalonia.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.Desktop;

public class DesktopApp : Avalonia.UI.App
{
    public static new IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        // 配置依赖注入
        var services = new ServiceCollection();
        services.AddAvaloniaServices();
        // 注册插件加载器
        services.AddSingleton<PluginLoader>();
        ServiceProvider = services.BuildServiceProvider();

        // 加载插件
        LoadPlugins();

        AvaloniaXamlLoader.Load(this);
        DataContext = new ApplicationViewModel();
    }

    private void LoadPlugins()
    {
        var navigationService = ServiceProvider?.GetRequiredService<INavigationService>();
        var menuConfigurationService = ServiceProvider?.GetRequiredService<IMenuConfigurationService>();

        if (navigationService != null && menuConfigurationService != null)
        {
            // 直接加载所有引用的插件
            var pluginTypes = new List<Type>
            {
                typeof(ButtonsInputsPlugin),
                typeof(DateTimePlugin),
                typeof(DialogFeedbacksPlugin),
                typeof(NavigationMenusPlugin),
                typeof(LayoutDisplayPlugin)
            };

            foreach (var pluginType in pluginTypes)
            {
                if (typeof(IPlugin).IsAssignableFrom(pluginType) && !pluginType.IsAbstract)
                {
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                    plugin.Initialize();

                    // 注册插件提供的导航项
                    var navigationItems = plugin.GetNavigationItems();
                    navigationService.RegisterNavigations(navigationItems);

                    // 注册插件提供的菜单项
                    var menuItems = plugin.GetMenuItems();
                    menuConfigurationService.RegisterMenuItems(menuItems);
                }
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MvvmSplashWindow()
            {
                DataContext = new SplashViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // 从依赖注入容器中获取导航服务和菜单配置服务
            var navigationService = ServiceProvider?.GetRequiredService<INavigationService>();
            var menuConfigurationService = ServiceProvider?.GetRequiredService<IMenuConfigurationService>();
            singleView.MainView = new SingleView()
            {
                DataContext = new MainViewViewModel(navigationService!, menuConfigurationService!),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
