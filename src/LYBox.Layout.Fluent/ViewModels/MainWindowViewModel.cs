using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using AvaloniaFluentUI.Locale;
using AvaloniaFluentUI.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LYBox.Layout.Fluent.Messages;
using LYBox.Layout.Fluent.Models;
using LYBox.Layout.Fluent.Services;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Services;
using LYBox.Plugin.Shared.ViewModels;
using LYBox.Layout.Core.Services;
using LYBox.Layout.Core.ViewModels;

namespace LYBox.Layout.Fluent.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public override string Title => "Avalonia Fluent UI LYBox.Layout.Fluent";

    public string Home => LocalizationService.Instance.GetString("Home");
    public string SearchWatermark => LocalizationService.Instance.GetString("MV_SearchWatermark");

    private readonly List<string> _history = new();

    // Fluent 内置页面工厂（保留原有 Gallery 页面）
    private readonly Dictionary<string, Func<ViewModelBase>> _viewModelFactories;
    private readonly Dictionary<string, ViewModelBase> _viewModels = new();

    // 插件系统服务（可为 null，表示未接入插件系统；在 InitializePluginSystem 中赋值，故非 readonly）
    private INavigationService? _pluginNavigationService;
    private IMenuConfigurationService? _pluginMenuService;

    /// <summary>
    /// 插件提供的菜单项集合（数据绑定到 NavigationView）
    /// </summary>
    public ObservableCollection<MenuItemViewModel> PluginMenus { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    private bool _canGoBack;

    [ObservableProperty]
    private object _navigationViewSelectedItem;

    partial void OnNavigationViewSelectedItemChanged(object value)
    {
        if (value is AvaloniaFluentUI.Controls.NavigationViewItem item)
        {
            TogglePage(item.Tag + "");

            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine($"Navigation Item Changed, ItemName: {item.Tag}");
            Console.WriteLine("------------------------------------------------------------");
        }
        else if (value is MenuItemViewModel pluginItem)
        {
            // 插件菜单项激活
            if (!string.IsNullOrEmpty(pluginItem.Key))
            {
                ActivatePluginPage(pluginItem.Key);
            }
        }
    }

    [ObservableProperty]
    private object? _currentViewModel;

    private readonly AppConfig? _config;

    public MainWindowViewModel(AppConfig? config)
    {
        _viewModels["Home"] = new HomeViewModel();

        JumpService.OnJumpToControl += (_, model) =>
        {
            TogglePage(model.Page);
            WeakReferenceMessenger.Default.Send(new JumpToControlMessage(model.Page, model.ControlName));
        };

        _viewModelFactories = new Dictionary<string, Func<ViewModelBase>>
        {
            { "Settings", () => new SettingsViewModel(config) },
        };

        _config = config;
        CurrentViewModel = _viewModels["Home"];

        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        AvaloniaFluentTheme.Instance.ThemeChanged += (_, _) =>
        {
            if (SelectedBorderColor == Colors.Transparent)
            {
                OnPropertyChanged(nameof(BorderBrush));
            }
        };
    }

    /// <summary>
    /// 接入插件系统：注入导航服务和菜单服务，加载插件菜单项
    /// </summary>
    public void InitializePluginSystem(INavigationService? navigationService, IMenuConfigurationService? menuService)
    {
        _pluginNavigationService = navigationService;
        _pluginMenuService = menuService;

        if (_pluginMenuService is not null)
        {
            var menus = _pluginMenuService.GetMenuStructure();
            foreach (var item in menus)
            {
                PluginMenus.Add(item);
            }
        }
    }

    /// <summary>
    /// 激活插件提供的页面
    /// </summary>
    private void ActivatePluginPage(string key)
    {
        if (_pluginNavigationService is null)
            return;

        try
        {
            CurrentViewModel = _pluginNavigationService.CreateViewModel(key);
        }
        catch (ArgumentOutOfRangeException)
        {
            Console.WriteLine($"Plugin navigation key not found: {key}");
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Home));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SearchWatermark));
    }

    private ViewModelBase GetOrCreateViewModel(string key)
    {
        if (_viewModels.TryGetValue(key, out var vm))
            return vm;

        if (_viewModelFactories.TryGetValue(key, out var factory))
        {
            vm = factory();
            _viewModels[key] = vm;
            return vm;
        }

        throw new KeyNotFoundException($"ViewModel not found for key: {key}");
    }

    public SettingsViewModel SettingsViewModel
    {
        get
        {
            if (!_viewModels.TryGetValue("Settings", out var vm))
            {
                vm = new SettingsViewModel(_config);
                _viewModels["Settings"] = vm;
            }
            return (SettingsViewModel)vm;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderWidth))]
    private int _selectedBorderWidthItem = 2;

    public int[] BorderWidthItems => [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] ;

    public Thickness BorderWidth => new Thickness(SelectedBorderWidthItem);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderBrush))]
    private Color _selectedBorderColor = Colors.Transparent;

    public IBrush BorderBrush => SelectedBorderColor == Colors.Transparent ? Brush.Parse(AvaloniaFluentTheme.Instance.IsDarkTheme ? "#484848" : "#D6D6D6") : new SolidColorBrush(SelectedBorderColor);

    [RelayCommand]
    private void ToggleTheme() => AvaloniaFluentTheme.Instance.ToggleTheme();

    [RelayCommand]
    private void TogglePage(string page)
    {
        // Introduction（工具箱）使用 Core 共享的 IntroductionDemoViewModel，
        // PluginManagement（插件管理）使用 Fluent 自有的 PluginManagementViewModel 副本。
        // 这两个 VM 均继承 LYBox.Plugin.Shared.ViewModelBase 而非 Fluent 的 ViewModelBase，
        // 因此无法放入 _viewModelFactories（类型为 Dictionary<string, Func<ViewModelBase>>），走独立创建路径。
        if (page == "Introduction")
        {
            CurrentViewModel = new IntroductionDemoViewModel();
            return;
        }

        if (page == "PluginManagement")
        {
            var pluginLoader = ServiceLocator.GetService<IPluginLoader>();
            var installationManager = ServiceLocator.GetService<IPluginInstallationManager>();
            if (pluginLoader is not null && installationManager is not null)
            {
                CurrentViewModel = new PluginManagementViewModel(pluginLoader, installationManager);
            }
            return;
        }

        ViewModelBase? target;
        try
        {
            target = GetOrCreateViewModel(page);
        }
        catch (KeyNotFoundException)
        {
            // 内置页面未找到，尝试插件导航
            if (_pluginNavigationService is not null)
            {
                try
                {
                    CurrentViewModel = _pluginNavigationService.CreateViewModel(page);
                    CanGoBack = _history.Count > 0;
                    return;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return;
                }
            }
            return;
        }

        if (target == CurrentViewModel) return;

        if (CurrentViewModel != null)
        {
            var currentPageKey = GetKeyByViewModel(CurrentViewModel);
            if (currentPageKey != null)
            {
                Console.WriteLine("------------------------------------------------------------");
                _history.Add(currentPageKey);
                Console.WriteLine($"Load To History: {currentPageKey}");
                Console.WriteLine("------------------------------------------------------------");
            }
        }

        CurrentViewModel = target;
        CanGoBack = _history.Count > 0;

#if DEBUG
        Debug.WriteLine($"Toggle Page To: {target}");
#endif
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        if (_history.Count <= 0)
            return;

        Console.WriteLine("Go Back");

        var last = _history[^1];
        _history.RemoveAt(_history.Count - 1);

        if (GetOrCreateViewModel(last) is { } view)
        {
            CurrentViewModel = view;

            Console.WriteLine($"Back, Tag: {last}, View: {view.Title}, Trigger Jump To ControlMessage");
        }

        WeakReferenceMessenger.Default.Send(new JumpToControlMessage(last, null));

        CanGoBack = _history.Count > 0;
    }

    private string? GetKeyByViewModel(object? vm)
    {
        if (vm is not ViewModelBase vmb) return null;
        foreach (var kvp in _viewModels)
        {
            if (kvp.Value == vmb) return kvp.Key;
        }
        return null;
    }
}
