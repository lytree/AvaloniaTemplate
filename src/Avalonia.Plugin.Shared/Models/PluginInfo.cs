namespace Avalonia.Plugin.Shared.Models;

public class PluginInfo
{
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = [];
    public string InstallPath { get; set; } = string.Empty;
    public string AssemblyPath { get; set; } = string.Empty;
    public PluginState State { get; set; } = PluginState.NotInstalled;
    public string? ErrorMessage { get; set; }
    public DateTime? InstallTime { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool HasMetadata { get; set; }
}
