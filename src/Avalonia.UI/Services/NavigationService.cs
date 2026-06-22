using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.UI.Pages;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public class NavigationService : INavigationService
{
    private readonly Dictionary<string, ViewModelFactory> _viewModelFactories = [];
    // 使用 WeakReference 缓存 ViewModel，当 ViewModel 仅被缓存引用时允许 GC 回收，
    // 避免导航历史中所有 ViewModel 永久驻留内存。
    private readonly Dictionary<string, WeakReference<object>> _viewModelCache = [];

    public NavigationService()
    {
        RegisterDefaultNavigations();
    }

    public void AttachPluginLoader(IPluginLoader pluginLoader)
    {
        pluginLoader.PluginUnloaded += OnPluginUnloaded;
    }

    private void OnPluginUnloaded(object? sender, PluginInfo pluginInfo)
    {
        InvalidateCache(pluginInfo.PluginId);
    }

    private void RegisterDefaultNavigations()
    {
        RegisterNavigation("Introduction", () => new IntroductionDemoViewModel());
        RegisterNavigation("AboutUs", () => new AboutUsDemoViewModel());
        RegisterNavigation("Settings", () => new SettingsPageViewModel(
            ServiceLocator.GetService<ISettingsService>(),
            ServiceLocator.GetService<ILocalizationService>()));
        RegisterNavigation("PluginManagement", () => new PluginManagementViewModel(
            ServiceLocator.GetService<IPluginLoader>(),
            ServiceLocator.GetService<IPluginInstallationManager>()));

        ViewLocator.Register<IntroductionDemoViewModel, IntroductionDemo>();
        ViewLocator.Register<AboutUsDemoViewModel, AboutUsDemo>();
        ViewLocator.Register<SettingsPageViewModel, SettingsPage>();
        ViewLocator.Register<PluginManagementViewModel, PluginManagementPage>();
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
        // 尝试从弱引用缓存命中
        if (_viewModelCache.TryGetValue(key, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var cached) && cached is not null)
            {
                return cached;
            }
            // 目标已被 GC 回收，清理失效条目
            _viewModelCache.Remove(key);
        }

        if (_viewModelFactories.TryGetValue(key, out var factory))
        {
            var viewModel = factory();
            _viewModelCache[key] = new WeakReference<object>(viewModel);
            return viewModel;
        }

        throw new System.ArgumentOutOfRangeException(nameof(key), key, null);
    }

    public void InvalidateCache(string key)
    {
        if (_viewModelCache.TryGetValue(key, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var viewModel))
            {
                ViewLocator.InvalidateViewCache(viewModel);
                (viewModel as IDisposable)?.Dispose();
            }
            _viewModelCache.Remove(key);
        }
    }

    public void InvalidateAllCache()
    {
        foreach (var weakRef in _viewModelCache.Values)
        {
            if (weakRef.TryGetTarget(out var viewModel))
            {
                ViewLocator.InvalidateViewCache(viewModel);
                (viewModel as IDisposable)?.Dispose();
            }
        }
        _viewModelCache.Clear();
    }

    public IEnumerable<string> GetNavigationKeys()
    {
        return _viewModelFactories.Keys;
    }
}
