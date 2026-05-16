using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.UI.ViewModels;

public partial class PluginManagementViewModel : ViewModelBase
{
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginInstallationManager _installationManager;

    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    [ObservableProperty] private PluginItemViewModel? _selectedPlugin;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _installProgress;
    [ObservableProperty] private bool _isInstalling;

    public PluginManagementViewModel(IPluginLoader pluginLoader, IPluginInstallationManager installationManager)
    {
        _pluginLoader = pluginLoader;
        _installationManager = installationManager;

        _installationManager.PluginInstalled += OnPluginInstalled;
        _installationManager.PluginUninstalled += OnPluginUninstalled;
        _pluginLoader.PluginStateChanged += OnPluginStateChanged;

        RefreshPlugins();
    }

    [RelayCommand]
    private void RefreshPlugins()
    {
        Plugins.Clear();
        var installedPlugins = _pluginLoader.GetInstalledPlugins();
        foreach (var plugin in installedPlugins.Where(p => p.HasMetadata))
        {
            Plugins.Add(new PluginItemViewModel(plugin));
        }
    }

    [RelayCommand]
    private async Task InstallPluginAsync()
    {
        var storageProvider = Avalonia.Controls.TopLevel.GetTopLevel(
            Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        if (storageProvider?.StorageProvider == null) return;

        var files = await storageProvider.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Plugin Package",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType("Plugin Package")
                {
                    Patterns = ["*.nupkg", "*.zip"]
                }
            ]
        });

        if (files.Count == 0) return;

        var filePath = files[0].Path.LocalPath;
        IsInstalling = true;
        InstallProgress = 0;

        var progress = new Progress<double>(p => InstallProgress = p * 100);
        var result = await _installationManager.InstallFromFileAsync(filePath, progress);

        IsInstalling = false;

        if (result.Success)
        {
            StatusMessage = $"Plugin '{result.PluginInfo?.Name}' installed successfully";
        }
        else
        {
            StatusMessage = $"Installation failed: {result.ErrorMessage}";
        }
    }

    [RelayCommand]
    private async Task UninstallPluginAsync(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null || pluginItem.IsBuiltIn) return;

        var success = await _installationManager.UninstallAsync(pluginItem.PluginId);
        if (success)
        {
            pluginItem.UpdateFrom(_pluginLoader.GetPlugin(pluginItem.PluginId) ?? new PluginInfo { PluginId = pluginItem.PluginId, State = PluginState.PendingUninstall });
            StatusMessage = $"Plugin '{pluginItem.Name}' will be uninstalled after restart";
        }
    }

    [RelayCommand]
    private void EnablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _installationManager.EnablePluginAsync(pluginItem.PluginId);
    }

    [RelayCommand]
    private void DisablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _installationManager.DisablePluginAsync(pluginItem.PluginId);
    }

    private void OnPluginInstalled(object? sender, PluginInfo e)
    {
        if (!e.HasMetadata) return;

        var existing = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
        if (existing != null)
        {
            existing.UpdateFrom(e);
        }
        else
        {
            Plugins.Add(new PluginItemViewModel(e));
        }
    }

    private void OnPluginUninstalled(object? sender, PluginInfo e)
    {
        var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
        if (item != null)
        {
            var updatedInfo = _pluginLoader.GetPlugin(e.PluginId);
            if (updatedInfo != null)
            {
                item.UpdateFrom(updatedInfo);
            }
        }
    }

    private void OnPluginStateChanged(object? sender, PluginInfo e)
    {
        if (!e.HasMetadata) return;

        var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
        if (item != null)
        {
            item.UpdateFrom(e);
        }
        else
        {
            Plugins.Add(new PluginItemViewModel(e));
        }
    }
}

public partial class PluginItemViewModel : ViewModelBase
{
    [ObservableProperty] private string _pluginId = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private PluginState _state;
    [ObservableProperty] private bool _isBuiltIn;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _stateText = string.Empty;
    [ObservableProperty] private string _stateColor = "#808080";
    [ObservableProperty] private bool _canEnable;
    [ObservableProperty] private bool _canDisable;
    [ObservableProperty] private bool _canUninstall;

    public PluginItemViewModel(PluginInfo info)
    {
        UpdateFrom(info);
    }

    public void UpdateFrom(PluginInfo info)
    {
        PluginId = info.PluginId;
        Name = info.Name;
        Version = info.Version;
        Author = info.Author;
        Description = info.Description;
        State = info.State;
        IsBuiltIn = info.IsBuiltIn;
        ErrorMessage = info.ErrorMessage;

        (StateText, StateColor) = info.State switch
        {
            PluginState.Loaded => ("Loaded", "#4CAF50"),
            PluginState.Installed => ("Installed", "#2196F3"),
            PluginState.Disabled => ("Disabled", "#FF9800"),
            PluginState.PendingUninstall => ("Pending Uninstall", "#9C27B0"),
            PluginState.Error => ("Error", "#F44336"),
            _ => ("Not Installed", "#808080")
        };

        CanEnable = info.State == PluginState.Disabled;
        CanDisable = info.State == PluginState.Loaded || info.State == PluginState.Installed;
        CanUninstall = !info.IsBuiltIn && info.State != PluginState.PendingUninstall;
    }
}
