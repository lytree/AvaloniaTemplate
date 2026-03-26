using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public class NavigationService : INavigationService
{
    private readonly Dictionary<string, ViewModelFactory> _viewModelFactories = new();

    public NavigationService()
    {
        // 注册默认导航项
        RegisterDefaultNavigations();
    }

    private void RegisterDefaultNavigations()
    {
        // 保留默认导航项：Introduction、AboutUs 和 Settings
        RegisterNavigation(Avalonia.Plugin.Shared.MenuKeys.MenuKeyIntroduction, () => new IntroductionDemoViewModel());
        RegisterNavigation(Avalonia.Plugin.Shared.MenuKeys.MenuKeyAboutUs, () => new AboutUsDemoViewModel());
        RegisterNavigation(Avalonia.Plugin.Shared.MenuKeys.MenuKeySettings, () => new SettingsPageViewModel());
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
