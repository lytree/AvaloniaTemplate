using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.Services;

namespace LYBox.Layout.Fluent.Services;

/// <summary>
/// Fluent 布局的导航服务实现。
/// 不注册任何默认导航项（Fluent 有自己的页面工厂），仅转发插件注册的导航项。
/// </summary>
public sealed class NavigationService : INavigationService
{
    private const int MaxCacheSize = 5;

    private readonly Dictionary<string, ViewModelFactory> _viewModelFactories = [];
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _viewModelCache = [];
    private readonly LinkedList<CacheEntry> _lruList = new();

    private sealed class CacheEntry(string key, object viewModel)
    {
        public string Key { get; } = key;
        public object ViewModel { get; } = viewModel;
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
        if (_viewModelCache.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            return node.Value.ViewModel;
        }

        if (!_viewModelFactories.TryGetValue(key, out var factory))
            throw new System.ArgumentOutOfRangeException(nameof(key), key, null);

        var viewModel = factory();

        if (_lruList.Count >= MaxCacheSize)
        {
            var lru = _lruList.Last!;
            _lruList.RemoveLast();
            _viewModelCache.Remove(lru.Value.Key);
            LYBox.Plugin.Shared.ViewLocator.InvalidateViewCache(lru.Value.ViewModel);
            (lru.Value.ViewModel as IDisposable)?.Dispose();
        }

        var newNode = _lruList.AddFirst(new CacheEntry(key, viewModel));
        _viewModelCache[key] = newNode;
        return viewModel;
    }

    public void InvalidateCache(string key)
    {
        if (_viewModelCache.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            _viewModelCache.Remove(key);
            LYBox.Plugin.Shared.ViewLocator.InvalidateViewCache(node.Value.ViewModel);
            (node.Value.ViewModel as IDisposable)?.Dispose();
        }
    }

    public void InvalidateAllCache()
    {
        foreach (var node in _lruList)
        {
            LYBox.Plugin.Shared.ViewLocator.InvalidateViewCache(node.ViewModel);
            (node.ViewModel as IDisposable)?.Dispose();
        }
        _lruList.Clear();
        _viewModelCache.Clear();
    }

    /// <summary>
    /// 订阅 PluginLoader 的 PluginUnloaded 事件，在插件卸载时失效对应缓存。
    /// </summary>
    public void AttachPluginLoader(IPluginLoader pluginLoader)
    {
        pluginLoader.PluginUnloaded += OnPluginUnloaded;
    }

    private void OnPluginUnloaded(object? sender, PluginInfo pluginInfo)
    {
        InvalidateCache(pluginInfo.PluginId);
    }

    public IEnumerable<string> GetNavigationKeys()
    {
        return _viewModelFactories.Keys;
    }
}
