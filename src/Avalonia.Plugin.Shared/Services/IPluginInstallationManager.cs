using Avalonia.Plugin.Shared.Models;

namespace Avalonia.Plugin.Shared.Services;

public interface IPluginInstallationManager
{
    Task<PluginInstallResult> InstallFromFileAsync(string packageFilePath, IProgress<double>? progress = null);
    Task<PluginInstallResult> InstallFromStreamAsync(Stream stream, string fileName, IProgress<double>? progress = null);
    Task<bool> UninstallAsync(string pluginId);
    Task<bool> EnablePluginAsync(string pluginId);
    Task<bool> DisablePluginAsync(string pluginId);
    string GetPluginInstallDirectory();
    string GetPluginDirectory(string pluginId);
    event EventHandler<PluginInfo>? PluginInstalled;
    event EventHandler<PluginInfo>? PluginUninstalled;
}

public class PluginInstallResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public PluginInfo? PluginInfo { get; set; }
}
