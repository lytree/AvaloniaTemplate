using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using LYBox.Layout.Core.ViewModels;
using LYBox.Layout.Fluent.Pages;
using LYBox.Layout.Fluent.ViewModels;
using LYBox.Layout.Fluent.Views;

namespace LYBox.Layout.Fluent;

public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Func<Control>> _factory = new();
    // 委托到 Plugin.Shared.ViewLocator 处理插件注册的 ViewModel→View 映射
    private static readonly LYBox.Plugin.Shared.ViewLocator _pluginViewLocator = new();

    public ViewLocator()
    {
        Register();
    }

    private void Register()
    {
        // Fluent 内置 Gallery 页面
        _factory[typeof(HomeViewModel)] = () => new HomeView();
        _factory[typeof(SettingsViewModel)] = () => new SettingsView();

        // Core 业务 VM → Fluent 风格 View 映射
        // IntroductionDemoViewModel 提取到 LYBox.Layout.Core.ViewModels，两个布局共享
        _factory[typeof(IntroductionDemoViewModel)] = () => new IntroductionPage();
        // PluginManagementViewModel 为 Fluent 布局独立副本（移除了 Ursa 包依赖），见 ViewModels/PluginManagementViewModel.cs
        _factory[typeof(PluginManagementViewModel)] = () => new PluginManagementPage();
    }

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var vmType = param.GetType();

        // 优先在 Fluent 内置工厂中查找（Gallery 页面）
        if (_factory.TryGetValue(vmType, out var creator))
            return creator();

        // 回退到 Plugin.Shared.ViewLocator（插件注册的 ViewModel→View 映射）
        return _pluginViewLocator.Build(param);
    }

    public bool Match(object? data)
    {
        // 匹配所有非 null 数据：内置 VM 由本工厂处理，插件 VM 由 Plugin.Shared 回退处理
        return data is not null;
    }
}
