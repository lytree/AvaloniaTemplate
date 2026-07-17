using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LYBox.Plugin.Shared;
using LYBox.Plugin.Shared.Models;
using LYBox.Plugin.Shared.Services;
using LYBox.Layout.Core.ViewModels;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LYBox.Layout.Fluent.ViewModels;

/// <summary>
/// Fluent 布局的插件管理 ViewModel。
/// 与 Ursa 布局的 PluginManagementViewModel 逻辑一致，但移除了对 Ursa.Controls.OverlayMessageBox 的依赖，
/// 安装失败时仅通过 StatusMessage 展示错误信息（Fluent 布局未引入 Irihi.Ursa 包）。
/// 共享的 PluginItemViewModel 已提取到 LYBox.Layout.Core.ViewModels。
/// 注意：此处显式继承 LYBox.Plugin.Shared.ViewModelBase（实现 IDisposable），
/// 而非 Fluent 自身的 ViewModelBase（无 IDisposable，且含 Title/LocalizationService 约定）。
/// </summary>
public partial class PluginManagementViewModel : LYBox.Plugin.Shared.ViewModelBase
{
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginInstallationManager _installationManager;
    private readonly ILocalizationService _localizationService;

    public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

    [ObservableProperty] private PluginItemViewModel? _selectedPlugin;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private double _installProgress;
    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private bool _needsRestart;

    public PluginManagementViewModel(IPluginLoader pluginLoader, IPluginInstallationManager installationManager)
    {
        _pluginLoader = pluginLoader;
        _installationManager = installationManager;
        _localizationService = ServiceLocator.GetService<ILocalizationService>();

        _installationManager.PluginInstalled += OnPluginInstalled;
        _installationManager.PluginUninstalled += OnPluginUninstalled;
        _installationManager.PluginUpgradeScheduled += OnPluginUpgradeScheduled;
        _pluginLoader.PluginStateChanged += OnPluginStateChanged;

        RefreshPlugins();
    }

    [RelayCommand]
    private void RefreshPlugins()
    {
        Plugins.Clear();
        var installedPlugins = _pluginLoader.GetInstalledPlugins();
        foreach (var plugin in installedPlugins)
        {
            Plugins.Add(new PluginItemViewModel(plugin, _localizationService));
        }

        NeedsRestart = installedPlugins.Any(p =>
            p.State == PluginState.PendingUninstall ||
            p.State == PluginState.PendingUpgrade ||
            p.State == PluginState.Installed);
    }

    /// <summary>
    /// 当前插件安装目录的绝对路径（用于 UI 展示）。
    /// </summary>
    public string PluginsDirectory => _installationManager.GetPluginInstallDirectory();

    /// <summary>
    /// 在系统文件管理器中打开插件目录。失败时通过 StatusMessage 展示错误。
    /// </summary>
    [RelayCommand]
    private void OpenPluginDirectory()
    {
        try
        {
            var dir = _installationManager.GetPluginInstallDirectory();
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage = _localizationService.GetString(
                "OPEN_DIR_FAILED",
                "打开目录失败：{0}",
                ex.Message);
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
            Title = _localizationService.GetString("SELECT_PLUGIN_PACKAGE", "Select Plugin Package"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new Avalonia.Platform.Storage.FilePickerFileType(_localizationService.GetString("PLUGIN_PACKAGE", "Plugin Package"))
                {
                    Patterns = ["*.zip"]
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
            // 区分新安装与升级调度两种场景的提示文案
            if (result.PluginInfo?.State == PluginState.PendingUpgrade)
            {
                StatusMessage = _localizationService.GetString(
                    "PLUGIN_UPGRADE_SCHEDULED",
                    "Plugin '{0}' upgrade scheduled, restart to apply",
                    result.PluginInfo?.Name ?? "");
            }
            else
            {
                StatusMessage = _localizationService.GetString(
                    "PLUGIN_INSTALLED_RESTART",
                    "Plugin '{0}' installed, restart to activate",
                    result.PluginInfo?.Name ?? "");
            }
            NeedsRestart = true;
        }
        else
        {
            // Fluent 布局未引入 Irihi.Ursa，安装失败仅通过 StatusMessage 展示错误原因
            var reason = result.ErrorMessage ?? "";
            StatusMessage = _localizationService.GetString("INSTALLATION_FAILED", reason);
        }
    }

    [RelayCommand]
    private async Task UninstallPluginAsync(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null || pluginItem.IsBuiltIn) return;

        var success = await _installationManager.UninstallAsync(pluginItem.PluginId);
        if (success)
        {
            pluginItem.UpdateFrom(_pluginLoader.GetPlugin(pluginItem.PluginId) ?? new PluginInfo { PluginId = pluginItem.PluginId, Name = pluginItem.Name, State = PluginState.PendingUninstall }, _localizationService);
            StatusMessage = _localizationService.GetString("PLUGIN_UNINSTALL_AFTER_RESTART", "Plugin '{0}' will be uninstalled after restart", pluginItem.Name);
            NeedsRestart = true;
        }
    }

    [RelayCommand]
    private async Task CancelUpgradeAsync(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;

        var success = await _installationManager.CancelUpgradeAsync(pluginItem.PluginId);
        if (success)
        {
            var updated = _pluginLoader.GetPlugin(pluginItem.PluginId);
            if (updated != null)
            {
                pluginItem.UpdateFrom(updated, _localizationService);
            }
            StatusMessage = _localizationService.GetString(
                "PLUGIN_UPGRADE_CANCELLED",
                "Plugin '{0}' upgrade cancelled",
                pluginItem.Name);

            // 取消后可能不再需要重启
            var installed = _pluginLoader.GetInstalledPlugins();
            NeedsRestart = installed.Any(p =>
                p.State == PluginState.PendingUninstall ||
                p.State == PluginState.PendingUpgrade ||
                p.State == PluginState.Installed);
        }
    }

    [RelayCommand]
    private void EnablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _installationManager.EnablePluginAsync(pluginItem.PluginId);
        StatusMessage = _localizationService.GetString("PLUGIN_ENABLE_RESTART", "Plugin '{0}' will be enabled after restart", pluginItem.Name);
        NeedsRestart = true;
    }

    [RelayCommand]
    private void DisablePlugin(PluginItemViewModel? pluginItem)
    {
        if (pluginItem == null) return;
        _ = _installationManager.DisablePluginAsync(pluginItem.PluginId);
        StatusMessage = _localizationService.GetString("PLUGIN_DISABLE_RESTART", "Plugin '{0}' will be disabled after restart", pluginItem.Name);
        NeedsRestart = true;
    }

    private void OnPluginInstalled(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (existing != null)
            {
                existing.UpdateFrom(e, _localizationService);
            }
            else
            {
                Plugins.Add(new PluginItemViewModel(e, _localizationService));
            }
        });
    }

    private void OnPluginUninstalled(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (item != null)
            {
                var updatedInfo = _pluginLoader.GetPlugin(e.PluginId);
                if (updatedInfo != null)
                {
                    item.UpdateFrom(updatedInfo, _localizationService);
                }
            }
            NeedsRestart = true;
        });
    }

    private void OnPluginStateChanged(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (item != null)
            {
                item.UpdateFrom(e, _localizationService);
            }
            else
            {
                Plugins.Add(new PluginItemViewModel(e, _localizationService));
            }
        });
    }

    private void OnPluginUpgradeScheduled(object? sender, PluginInfo e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = Plugins.FirstOrDefault(p => p.PluginId == e.PluginId);
            if (item != null)
            {
                item.UpdateFrom(e, _localizationService);
            }
            NeedsRestart = true;
        });
    }

    public override void Dispose()
    {
        _installationManager.PluginInstalled -= OnPluginInstalled;
        _installationManager.PluginUninstalled -= OnPluginUninstalled;
        _installationManager.PluginUpgradeScheduled -= OnPluginUpgradeScheduled;
        _pluginLoader.PluginStateChanged -= OnPluginStateChanged;
        base.Dispose();
    }
}
