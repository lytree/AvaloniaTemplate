using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Plugin.Shared;
using Avalonia.UI.Services;
using Avalonia.UI.ViewModels;
using Avalonia.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Avalonia.Desktop;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    public App()
    {
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 配置依赖注入
        var services = new ServiceCollection();
        services.AddAvaloniaServices();
        ServiceProvider = services.BuildServiceProvider();

        // 初始化服务定位器
        ServiceLocator.Initialize(ServiceProvider);

        // 加载插件
        LoadPlugins();

        DataContext = new ApplicationViewModel();
    }

    private void LoadPlugins()
    {
        var navigationService = ServiceProvider?.GetRequiredService<INavigationService>();
        var menuConfigurationService = ServiceProvider?.GetRequiredService<IMenuConfigurationService>();

        if (navigationService != null && menuConfigurationService != null)
        {
            // 动态加载插件
            var pluginTypes = FindPluginTypes();

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;

                    // 注册插件提供的导航项
                    var navigationItems = plugin.GetNavigationItems();
                    navigationService.RegisterNavigations(navigationItems);

                    // 注册插件提供的菜单项
                    var menuItems = plugin.GetMenuItems();
                    menuConfigurationService.RegisterMenuItems(menuItems);
                }
                catch (Exception ex)
                {
                    // 记录错误信息
                    Console.WriteLine($"Error initializing plugin {pluginType.Name}: {ex.Message}");
                }
            }
        }
    }

    private List<Type> FindPluginTypes()
    {
        var pluginTypes = new List<Type>();

        // 3. 从程序目录查找插件类型
        var AppDirectory = Path.Combine(AppContext.BaseDirectory);
        if (Directory.Exists(AppDirectory))
        {
            var dllFiles = Directory.GetFiles(AppDirectory, "*.dll");
            foreach (var dllFile in dllFiles)
            {
                try
                {
                    var assembly = SafeLoad(dllFile);
                    pluginTypes.AddRange(FindPluginsInAssembly(assembly));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading plugin {dllFile}: {ex.Message}");
                }
            }
        }

        // 3. 从插件目录查找插件类型
        var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (Directory.Exists(pluginsDirectory))
        {
            var dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll");
            foreach (var dllFile in dllFiles)
            {
                try
                {
                    var assembly = SafeLoad(dllFile);
                    pluginTypes.AddRange(FindPluginsInAssembly(assembly));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading plugin {dllFile}: {ex.Message}");
                }
            }
        }

        return pluginTypes;
    }

    private IEnumerable<Type> FindPluginsInAssembly(Assembly assembly)
    {
        var pluginTypes = new List<Type>();

        try
        {
            var types = assembly.GetTypes();
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                {
                    pluginTypes.Add(type);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning assembly {assembly.FullName}: {ex.Message}");
        }

        return pluginTypes;
    }
    public Assembly SafeLoad(string dllPath)
    {
        // 1. 获取要加载的程序集的名称（不加载文件，只读元数据）
        AssemblyName targetName = AssemblyName.GetAssemblyName(dllPath);

        // 2. 检查当前已加载的程序集
        var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), targetName));

        if (loadedAssembly != null)
        {
            return loadedAssembly; // 已存在，直接返回
        }

        // 3. 不存在则加载
        return Assembly.LoadFrom(dllPath);
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
