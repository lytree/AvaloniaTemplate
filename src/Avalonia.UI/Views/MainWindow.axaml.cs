using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Views;

public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; } = new();
    public MainWindow()
    {
        InitializeComponent();
    }
    private async void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.SelectedPageInfo)) return;
        if (!IsLoaded || !ViewModel.IsRendered)
            return;
        await CoreNavigate(ViewModel.SelectedPageInfo);
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {

    }
    protected override async void OnOpened(EventArgs e)
    { }

    private async void NavigationListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }
    private void SettingsWindowNew_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel.IsViewCompressed = Width < 800;
        if (WindowState == WindowState.Maximized)
            ViewModel.IsViewCompressed = false;
        if (!ViewModel.IsViewCompressed)
        {
            ViewModel.IsNavigationDrawerOpened = true;
        }
    }
    private void ButtonBaseToggleNavigationDrawer_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsNavigationDrawerOpened = !ViewModel.IsNavigationDrawerOpened;
    }

    private void ButtonGoBack_OnClick(object sender, RoutedEventArgs e)
    {
        NavigationFrame.GoBack();
    }
    private void SettingsWindowNew_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            return;
        }
        e.Cancel = true;
        IsOpened = false;
        Hide();
        SettingsService.SaveSettings("关闭应用设置窗口");
        ComponentsService.SaveConfig();
        App.GetService<IAutomationService>().SaveConfig("关闭应用设置窗口");
        if (SettingsService.Settings.SettingsPagesCachePolicy <= 1)
        {
            _cachedPages.Clear();
        }
        GC.Collect();
    }

    private void CommandBindingOpenDrawer_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ViewModel.DrawerContent = e.Parameter;
        ViewModel.IsDrawerOpen = true;
    }

    private void CommandBindingCloseDrawer_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ViewModel.IsDrawerOpen = false;
    }

    private void ButtonRestartApp_OnClick(object sender, RoutedEventArgs e)
    {
        ShowRestartDialog();
    }
    private void CommandBindingRestartApp_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ViewModel.IsRequestedRestart = true;
        ShowRestartDialog();
    }

    private void PopupButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsPopupOpen = false;
    }

    private void OpenDrawer(string key)
    {
        ViewModel.DrawerContent = this.FindResource(key);
        ViewModel.IsDrawerOpen = true;
    }

    private void MenuItemExperimentalSettings_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsPopupOpen = false;
        OpenDrawer("ExperimentalSettings");
    }

    private async void MenuItemExitManagement_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ManagementService.ExitManagementAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "无法退出管理。");
            _ = CommonTaskDialogs.ShowDialog("无法退出管理", $"无法退出管理：{ex.Message}", this);
        }
    }

    private void MenuItemAppLogs_OnClick(object sender, RoutedEventArgs e)
    {
        App.GetService<AppLogsWindow>().Open();
    }
    private void MenuItemOpenLogFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = Path.GetFullPath(CommonDirectories.AppLogFolderPath) ?? "",
            UseShellExecute = true
        });
    }

    private void MenuItemOpenAppFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = Path.GetFullPath(".") ?? "",
            UseShellExecute = true
        });
    }

    private void MenuItemDebugWindowRule_OnClick(object sender, RoutedEventArgs e)
    {
        IAppHost.GetService<WindowRuleDebugWindow>().Show();
    }

    private void MenuItemOpenDataFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = Path.GetFullPath(CommonDirectories.AppRootFolderPath) ?? "",
            UseShellExecute = true
        });
    }
    private async void NavigationView_OnItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.InvokedItemContainer is NavigationViewItem navItem && navItem.Tag is SettingsPageInfo info)
        {
            await CoreNavigate(info);
        }
    }

    private void NavigationView_OnBackRequested(object? sender, NavigationViewBackRequestedEventArgs e)
    {
        NavigationFrame.GoBack();
    }

    private void TogglePaneButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NavigationView.IsPaneOpen = !NavigationView.IsPaneOpen;
    }

    private void MenuItemDataTransfer_OnClick(object? sender, RoutedEventArgs e)
    {
        IAppHost.GetService<DataTransferWindow>().ShowDialog(this);
    }

    private void MenuItemOpenManagementSettingsPage_OnClick(object? sender, RoutedEventArgs e)
    {
        IAppHost.GetService<IUriNavigationService>().NavigateWrapped(new Uri("classisland://app/settings/management?ci_keepHistory=true"));
    }

    private void MenuItemJoinManagement_OnClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new JoinManagementDialog();
        dialog.ShowDialog((TopLevel.GetTopLevel(this) as Window)!);
    }

    private void BackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        NavigationFrame.GoBack();
    }

    private void SelectNavigationItem(SettingsPageInfo info)
    {
        var item = ViewModel.FlattenNavigationItemsCache
            .FirstOrDefault(x => Equals(x.Tag, info));
        if (NavigationView.MenuItems.Contains(item))
        {
            NavigationView.SelectedItem = item;
        }
        else if (NavigationView.MenuItems
                   .OfType<NavigationViewItem>()
                   .FirstOrDefault(x => x.MenuItems.Contains(item))
                   is { } parent)
        {
            parent.IsChildSelected = true;
            var isFirstNavigated = _isFirstNavigated;
            Dispatcher.UIThread.Post(() =>
            {
                if (!isFirstNavigated)
                {
                    parent.IsExpanded = true;
                }
                NavigationView.SelectedItem = item;
            });
        }

        foreach (var i in ViewModel.FlattenNavigationItemsCache.Where(x => !Equals(x.Tag, info)))
        {
            i.IsSelected = false;
        }
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {

    }

}