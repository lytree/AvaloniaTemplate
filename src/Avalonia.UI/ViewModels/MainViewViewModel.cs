using System;
using Avalonia.Controls.Notifications;
using Avalonia.Plugin.Shared;
using Avalonia.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Notification = Ursa.Controls.Notification;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace Avalonia.UI.ViewModels;

public partial class MainViewViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IMenuConfigurationService _menuConfigurationService;

    public WindowNotificationManager? NotificationManager { get; set; }
    public MenuViewModel Menus { get; }

    [ObservableProperty] private object? _content;

    [RelayCommand]
    public void Activate(string key)
    {
        OnNavigation(this, key);
    }

    public MainViewViewModel(INavigationService navigationService, IMenuConfigurationService menuConfigurationService)
    {
        _navigationService = navigationService;
        _menuConfigurationService = menuConfigurationService;
        Menus = _menuConfigurationService.GetMenuStructure();
        WeakReferenceMessenger.Default.Register<MainViewViewModel, string, string>(this, "JumpTo", OnNavigation);
        OnNavigation(this, "Introduction");
    }

    private void OnNavigation(MainViewViewModel vm, string s)
    {
        Content = _navigationService.CreateViewModel(s);
    }
}
