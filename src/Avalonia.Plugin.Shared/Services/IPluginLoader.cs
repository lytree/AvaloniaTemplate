using Avalonia.Plugin.Shared.Models;

namespace Avalonia.Plugin.Shared.Services;

public interface IPluginLoader
{
    IReadOnlyList<PluginInfo> GetInstalledPlugins();
    PluginInfo? GetPlugin(string pluginId);
    PluginLoadResult LoadPlugin(PluginInfo pluginInfo);
    void UnloadPlugin(string pluginId);
    void LoadAllPlugins();
    IPlugin? GetLoadedPlugin(string pluginId);
    IPluginMetadata? GetLoadedMetadata(string pluginId);
    event EventHandler<PluginInfo>? PluginLoaded;
    event EventHandler<PluginInfo>? PluginUnloaded;
    event EventHandler<PluginInfo>? PluginStateChanged;
}

public class PluginLoadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IPlugin? Plugin { get; set; }
    public IPluginMetadata? Metadata { get; set; }
}
