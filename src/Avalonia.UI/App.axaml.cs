using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.UI.Services;
using Avalonia.UI.ViewModels;
using Avalonia.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Avalonia.UI;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        // 配置依赖注入
        var services = new ServiceCollection();
        services.AddAvaloniaServices();
        ServiceProvider = services.BuildServiceProvider();

        AvaloniaXamlLoader.Load(this);
        DataContext = new ApplicationViewModel();
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
