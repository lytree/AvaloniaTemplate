using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.UI.Pages;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public class NavigationService : INavigationService
{
    private readonly Dictionary<string, ViewModelFactory> _viewModelFactories = [];

    public NavigationService()
    {
        // 注册默认导航项
        RegisterDefaultNavigations();
    }

    private void RegisterDefaultNavigations()
    {
        // 保留默认导航项：Introduction、AboutUs 和 Settings
        RegisterNavigation("Introduction", () => new IntroductionDemoViewModel());
        RegisterNavigation("AboutUs", () => new AboutUsDemoViewModel());
        RegisterNavigation("Settings", () => new SettingsPageViewModel());
        RegisterNavigation("WebPage", () => new WebPageDemoViewModel());

        ViewLocator.Register<IntroductionDemoViewModel, IntroductionDemo>();
        ViewLocator.Register<AboutUsDemoViewModel, AboutUsDemo>();
        ViewLocator.Register<SettingsPageViewModel, SettingsPage>();
        ViewLocator.Register<WebPageDemoViewModel, WebPageDemo>();
    }

    public void RegisterNavigation(string key, ViewModelFactory factory)
    {
        _viewModelFactories[key] = factory;
    }

    public void RegisterNavigations(Dictionary<string, ViewModelFactory> navigations)
    {
        foreach (var (key, factory) in navigations)
        {
            _viewModelFactories[key] = factory;
        }
    }

    public object CreateViewModel(string key)
    {
        if (_viewModelFactories.TryGetValue(key, out var factory))
        {
            return factory();
        }
        throw new System.ArgumentOutOfRangeException(nameof(key), key, null);
    }

    public IEnumerable<string> GetNavigationKeys()
    {
        return _viewModelFactories.Keys;
    }
}
