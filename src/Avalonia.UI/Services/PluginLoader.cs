using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;

namespace Avalonia.UI.Services;

public class PluginLoader : IPluginLoader, IDisposable
{
    public const string ExtraPluginEnvironmentVariableName = "AVALONIA_EXTRA_PLUGINS_PATH";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dictionary<string, PluginInfo> _pluginRegistry = [];
    private readonly Dictionary<string, AssemblyLoadContext> _loadContexts = [];
    private readonly Dictionary<string, IPlugin> _loadedPlugins = [];
    private readonly Dictionary<string, IPluginMetadata> _loadedMetadata = [];
    private readonly string _pluginsDirectory;
    private readonly string? _extraPluginPath;
    private readonly object _lock = new();

    public event EventHandler<PluginInfo>? PluginLoaded;
    public event EventHandler<PluginInfo>? PluginUnloaded;
    public event EventHandler<PluginInfo>? PluginStateChanged;

    public PluginLoader(string? pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        _extraPluginPath = Environment.GetEnvironmentVariable(ExtraPluginEnvironmentVariableName);
        Directory.CreateDirectory(_pluginsDirectory);
        ProcessPendingUninstalls();
        LoadAllPluginManifests();
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        lock (_lock)
        {
            return _pluginRegistry.Values.ToList();
        }
    }

    public PluginInfo? GetPlugin(string pluginId)
    {
        lock (_lock)
        {
            return _pluginRegistry.TryGetValue(pluginId, out var info) ? info : null;
        }
    }

    public PluginLoadResult LoadPlugin(PluginInfo pluginInfo)
    {
        lock (_lock)
        {
            if (_loadedPlugins.ContainsKey(pluginInfo.PluginId))
            {
                return new PluginLoadResult
                {
                    Success = true,
                    Plugin = _loadedPlugins[pluginInfo.PluginId],
                    Metadata = _loadedMetadata.GetValueOrDefault(pluginInfo.PluginId)
                };
            }

            if (pluginInfo.State == PluginState.Disabled || pluginInfo.State == PluginState.PendingUninstall)
            {
                return new PluginLoadResult
                {
                    Success = false,
                    ErrorMessage = $"Plugin is {pluginInfo.State}, cannot load"
                };
            }
        }

        if (!File.Exists(pluginInfo.AssemblyPath))
        {
            lock (_lock)
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = $"Assembly not found: {pluginInfo.AssemblyPath}";
                SavePluginManifest(pluginInfo);
                PluginStateChanged?.Invoke(this, pluginInfo);
            }
            return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
        }

        lock (_lock)
        {
            if (!ValidateDependencies(pluginInfo, out var depError))
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = depError;
                SavePluginManifest(pluginInfo);
                PluginStateChanged?.Invoke(this, pluginInfo);
                return new PluginLoadResult { Success = false, ErrorMessage = depError };
            }
        }

        AssemblyLoadContext loadContext;
        IPlugin? plugin = null;
        IPluginMetadata? metadata = null;

        try
        {
            loadContext = new PluginLoadContext(pluginInfo.AssemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(pluginInfo.AssemblyPath);

            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                if (typeof(IPlugin).IsAssignableFrom(type) && plugin == null)
                {
                    plugin = (IPlugin)Activator.CreateInstance(type)!;
                }

                if (typeof(IPluginMetadata).IsAssignableFrom(type) && metadata == null)
                {
                    metadata = (IPluginMetadata)Activator.CreateInstance(type)!;
                    metadata.Initialize();
                }

                if (plugin != null && metadata != null) break;
            }
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = $"Failed to load plugin: {ex.Message}";
                SavePluginManifest(pluginInfo);
                PluginStateChanged?.Invoke(this, pluginInfo);
            }
            return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
        }

        if (plugin == null)
        {
            loadContext.Unload();
            lock (_lock)
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = "No IPlugin implementation found in assembly";
                SavePluginManifest(pluginInfo);
                PluginStateChanged?.Invoke(this, pluginInfo);
            }
            return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
        }

        lock (_lock)
        {
            _loadContexts[pluginInfo.PluginId] = loadContext;
            _loadedPlugins[pluginInfo.PluginId] = plugin;
            if (metadata != null)
            {
                _loadedMetadata[pluginInfo.PluginId] = metadata;
                pluginInfo.HasMetadata = true;
            }

            pluginInfo.State = PluginState.Loaded;
            pluginInfo.ErrorMessage = null;
            SavePluginManifest(pluginInfo);

            PluginLoaded?.Invoke(this, pluginInfo);
            PluginStateChanged?.Invoke(this, pluginInfo);

            return new PluginLoadResult { Success = true, Plugin = plugin, Metadata = metadata };
        }
    }

    public void UnloadPlugin(string pluginId)
    {
        lock (_lock)
        {
            if (!_loadedPlugins.ContainsKey(pluginId)) return;

            _loadedPlugins.Remove(pluginId);
            _loadedMetadata.Remove(pluginId);

            if (_loadContexts.TryGetValue(pluginId, out var context))
            {
                context.Unload();
                _loadContexts.Remove(pluginId);
            }

            if (_pluginRegistry.TryGetValue(pluginId, out var info))
            {
                info.State = PluginState.Installed;
                SavePluginManifest(info);
                PluginUnloaded?.Invoke(this, info);
                PluginStateChanged?.Invoke(this, info);
            }
        }
    }

    public void DisablePlugin(string pluginId)
    {
        lock (_lock)
        {
            if (!_pluginRegistry.TryGetValue(pluginId, out var info)) return;

            if (_loadedPlugins.ContainsKey(pluginId))
            {
                _loadedPlugins.Remove(pluginId);
                _loadedMetadata.Remove(pluginId);

                if (_loadContexts.TryGetValue(pluginId, out var context))
                {
                    context.Unload();
                    _loadContexts.Remove(pluginId);
                }
            }

            info.State = PluginState.Disabled;
            info.ErrorMessage = null;
            SavePluginManifest(info);
            PluginUnloaded?.Invoke(this, info);
            PluginStateChanged?.Invoke(this, info);
        }
    }

    public void EnablePlugin(string pluginId)
    {
        lock (_lock)
        {
            if (!_pluginRegistry.TryGetValue(pluginId, out var info)) return;
            if (info.State != PluginState.Disabled) return;

            info.State = PluginState.Installed;
            SavePluginManifest(info);
            PluginStateChanged?.Invoke(this, info);

            LoadPlugin(info);
        }
    }

    public void MarkForUninstall(string pluginId)
    {
        lock (_lock)
        {
            if (!_pluginRegistry.TryGetValue(pluginId, out var info)) return;
            if (info.IsBuiltIn) return;

            if (_loadedPlugins.ContainsKey(pluginId))
            {
                _loadedPlugins.Remove(pluginId);
                _loadedMetadata.Remove(pluginId);

                if (_loadContexts.TryGetValue(pluginId, out var context))
                {
                    context.Unload();
                    _loadContexts.Remove(pluginId);
                }
            }

            info.State = PluginState.PendingUninstall;
            info.ErrorMessage = null;
            SavePluginManifest(info);
            PluginUnloaded?.Invoke(this, info);
            PluginStateChanged?.Invoke(this, info);
        }
    }

    public void LoadAllPlugins()
    {
        lock (_lock)
        {
            foreach (var info in _pluginRegistry.Values)
            {
                if (info.State == PluginState.Installed || info.State == PluginState.Error)
                {
                    LoadPlugin(info);
                }
            }

            LoadExtraPlugins();
        }
    }

    private void LoadExtraPlugins()
    {
        if (string.IsNullOrWhiteSpace(_extraPluginPath) || !Directory.Exists(_extraPluginPath))
            return;

        foreach (var dllPath in Directory.GetFiles(_extraPluginPath, "*.dll", SearchOption.TopDirectoryOnly))
        {
            TryLoadExtraPluginDll(dllPath);
        }

        foreach (var subDir in Directory.GetDirectories(_extraPluginPath))
        {
            var dirName = Path.GetFileName(subDir);
            var candidateDll = Path.Combine(subDir, $"{dirName}.dll");
            if (File.Exists(candidateDll))
            {
                TryLoadExtraPluginDll(candidateDll);
            }
        }
    }

    private void TryLoadExtraPluginDll(string dllPath)
    {
        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(dllPath);
            var pluginId = assemblyName.Name ?? Path.GetFileNameWithoutExtension(dllPath);

            if (_loadedPlugins.ContainsKey(pluginId))
                return;

            var pluginInfo = new PluginInfo
            {
                PluginId = pluginId,
                Name = assemblyName.Name ?? pluginId,
                Version = assemblyName.Version?.ToString() ?? "0.0.0",
                AssemblyPath = dllPath,
                InstallPath = Path.GetDirectoryName(dllPath) ?? _extraPluginPath,
                State = PluginState.Installed,
                IsBuiltIn = false
            };

            _pluginRegistry[pluginId] = pluginInfo;
            LoadPlugin(pluginInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load extra plugin from '{dllPath}': {ex.Message}");
        }
    }

    public void RegisterPlugin(PluginInfo pluginInfo)
    {
        lock (_lock)
        {
            _pluginRegistry[pluginInfo.PluginId] = pluginInfo;
            SavePluginManifest(pluginInfo);
        }
    }

    public void UnregisterPlugin(string pluginId)
    {
        lock (_lock)
        {
            UnloadPlugin(pluginId);
            _pluginRegistry.Remove(pluginId);
            DeletePluginManifest(pluginId);
        }
    }

    public IPlugin? GetLoadedPlugin(string pluginId)
    {
        lock (_lock)
        {
            return _loadedPlugins.GetValueOrDefault(pluginId);
        }
    }

    public IPluginMetadata? GetLoadedMetadata(string pluginId)
    {
        lock (_lock)
        {
            return _loadedMetadata.GetValueOrDefault(pluginId);
        }
    }

    private bool ValidateDependencies(PluginInfo pluginInfo, out string? error)
    {
        foreach (var depId in pluginInfo.Dependencies)
        {
            if (!_pluginRegistry.TryGetValue(depId, out var depInfo))
            {
                error = $"Missing dependency: {depId}";
                return false;
            }

            if (depInfo.State != PluginState.Loaded)
            {
                error = $"Dependency not loaded: {depId} ({depInfo.Name})";
                return false;
            }
        }

        error = null;
        return true;
    }

    private void ProcessPendingUninstalls()
    {
        if (!Directory.Exists(_pluginsDirectory)) return;

        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
                if (manifest == null) continue;

                if (manifest.State == nameof(PluginState.PendingUninstall))
                {
                    try
                    {
                        Directory.Delete(pluginDir, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete plugin directory '{pluginDir}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process plugin manifest '{manifestPath}': {ex.Message}");
            }
        }
    }

    private void LoadAllPluginManifests()
    {
        if (!Directory.Exists(_pluginsDirectory)) return;

        foreach (var pluginDir in Directory.GetDirectories(_pluginsDirectory))
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
                if (manifest == null) continue;

                var pluginInfo = ManifestToPluginInfo(manifest, pluginDir);

                if (pluginInfo.State == PluginState.Loaded)
                {
                    pluginInfo.State = PluginState.Installed;
                }

                _pluginRegistry[pluginInfo.PluginId] = pluginInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load plugin manifest '{manifestPath}': {ex.Message}");
            }
        }
    }

    private PluginInfo ManifestToPluginInfo(PluginManifest manifest, string pluginDir)
    {
        var assemblyPath = !string.IsNullOrEmpty(manifest.Assembly)
            ? Path.Combine(pluginDir, manifest.Assembly)
            : string.Empty;

        return new PluginInfo
        {
            PluginId = manifest.PluginId ?? string.Empty,
            Name = manifest.Name ?? string.Empty,
            Version = manifest.Version ?? "1.0.0",
            Author = manifest.Author ?? string.Empty,
            Description = manifest.Description ?? string.Empty,
            Dependencies = manifest.Dependencies ?? [],
            InstallPath = pluginDir,
            AssemblyPath = assemblyPath,
            State = Enum.TryParse<PluginState>(manifest.State, out var state) ? state : PluginState.Installed,
            InstallTime = manifest.InstallTime,
            IsBuiltIn = manifest.IsBuiltIn,
            HasMetadata = !string.IsNullOrEmpty(manifest.PluginId)
        };
    }

    private void SavePluginManifest(PluginInfo pluginInfo)
    {
        try
        {
            var pluginDir = pluginInfo.InstallPath;
            if (string.IsNullOrEmpty(pluginDir))
            {
                pluginDir = Path.Combine(_pluginsDirectory, pluginInfo.PluginId);
                pluginInfo.InstallPath = pluginDir;
            }

            Directory.CreateDirectory(pluginDir);

            var manifest = new PluginManifest
            {
                PluginId = pluginInfo.PluginId,
                Name = pluginInfo.Name,
                Version = pluginInfo.Version,
                Author = pluginInfo.Author,
                Description = pluginInfo.Description,
                Assembly = !string.IsNullOrEmpty(pluginInfo.AssemblyPath)
                    ? Path.GetFileName(pluginInfo.AssemblyPath)
                    : $"{pluginInfo.Name}.dll",
                Dependencies = pluginInfo.Dependencies,
                State = pluginInfo.State.ToString(),
                InstallTime = pluginInfo.InstallTime,
                IsBuiltIn = pluginInfo.IsBuiltIn
            };

            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            File.WriteAllText(manifestPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save plugin manifest for '{pluginInfo.PluginId}': {ex.Message}");
        }
    }

    private void DeletePluginManifest(string pluginId)
    {
        if (!_pluginRegistry.TryGetValue(pluginId, out var info)) return;

        try
        {
            var manifestPath = Path.Combine(info.InstallPath, "plugin.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete plugin manifest for '{pluginId}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var context in _loadContexts.Values)
            {
                try { context.Unload(); } catch { }
            }
            _loadContexts.Clear();
            _loadedPlugins.Clear();
            _loadedMetadata.Clear();
        }
    }

    private class PluginManifest
    {
        public string? PluginId { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string? Assembly { get; set; }
        public List<string>? Dependencies { get; set; }
        public string? State { get; set; }
        public DateTime? InstallTime { get; set; }
        public bool IsBuiltIn { get; set; }
    }
}
