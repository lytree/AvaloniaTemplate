namespace Avalonia.Plugin.Shared.Models;

public class PluginManifest
{
    public string? PluginId { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Assembly { get; set; }
    public List<string>? Dependencies { get; set; }

    /// <summary>
    /// Additional assembly name patterns that this plugin declares as shared
    /// (forwarded to the host's default AssemblyLoadContext). Each entry is either
    /// an exact assembly name or a prefix pattern ending with '*'.
    /// These are merged with the default shared-assemblies.txt list at runtime.
    /// </summary>
    public List<string>? SharedAssemblies { get; set; }

    public string? State { get; set; }
    public DateTime? InstallTime { get; set; }
    public bool IsBuiltIn { get; set; }
}
