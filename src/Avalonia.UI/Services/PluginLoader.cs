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

    private readonly Dictionary<string, PluginInfo> _pluginRegistry = [];
    private readonly Dictionary<string, AssemblyLoadContext> _loadContexts = [];
    private readonly Dictionary<string, IPlugin> _loadedPlugins = [];
    private readonly Dictionary<string, IPluginMetadata> _loadedMetadata = [];
    private readonly string _pluginsDirectory;
    private readonly string _registryFilePath;
    private readonly string? _extraPluginPath;
    private readonly object _lock = new();

    public event EventHandler<PluginInfo>? PluginLoaded;
    public event EventHandler<PluginInfo>? PluginUnloaded;
    public event EventHandler<PluginInfo>? PluginStateChanged;

    public PluginLoader(string? pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        _registryFilePath = Path.Combine(_pluginsDirectory, "plugin_registry.json");
        _extraPluginPath = Environment.GetEnvironmentVariable(ExtraPluginEnvironmentVariableName);
        Directory.CreateDirectory(_pluginsDirectory);
        LoadRegistry();
    }

    public IReadOnlyList<PluginInfo> GetInstalledPlugins()
    {
        lock (_lock)
        {
            return _pluginRegistry.Values.ToList().AsReadOnly();
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

            if (!File.Exists(pluginInfo.AssemblyPath))
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = $"Assembly not found: {pluginInfo.AssemblyPath}";
                SaveRegistry();
                PluginStateChanged?.Invoke(this, pluginInfo);
                return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
            }

            if (!ValidateDependencies(pluginInfo, out var depError))
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = depError;
                SaveRegistry();
                PluginStateChanged?.Invoke(this, pluginInfo);
                return new PluginLoadResult { Success = false, ErrorMessage = depError };
            }

            try
            {
                var loadContext = new PluginLoadContext(pluginInfo.AssemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(pluginInfo.AssemblyPath);

                IPlugin? plugin = null;
                IPluginMetadata? metadata = null;

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

                if (plugin == null)
                {
                    loadContext.Unload();
                    pluginInfo.State = PluginState.Error;
                    pluginInfo.ErrorMessage = "No IPlugin implementation found in assembly";
                    SaveRegistry();
                    PluginStateChanged?.Invoke(this, pluginInfo);
                    return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
                }

                _loadContexts[pluginInfo.PluginId] = loadContext;
                _loadedPlugins[pluginInfo.PluginId] = plugin;
                if (metadata != null)
                {
                    _loadedMetadata[pluginInfo.PluginId] = metadata;
                    pluginInfo.HasMetadata = true;
                }

                pluginInfo.State = PluginState.Loaded;
                pluginInfo.ErrorMessage = null;
                SaveRegistry();

                PluginLoaded?.Invoke(this, pluginInfo);
                PluginStateChanged?.Invoke(this, pluginInfo);

                return new PluginLoadResult { Success = true, Plugin = plugin, Metadata = metadata };
            }
            catch (Exception ex)
            {
                pluginInfo.State = PluginState.Error;
                pluginInfo.ErrorMessage = $"Failed to load plugin: {ex.Message}";
                SaveRegistry();
                PluginStateChanged?.Invoke(this, pluginInfo);
                return new PluginLoadResult { Success = false, ErrorMessage = pluginInfo.ErrorMessage };
            }
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
                SaveRegistry();
                PluginUnloaded?.Invoke(this, info);
                PluginStateChanged?.Invoke(this, info);
            }
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
            SaveRegistry();
        }
    }

    public void UnregisterPlugin(string pluginId)
    {
        lock (_lock)
        {
            UnloadPlugin(pluginId);
            _pluginRegistry.Remove(pluginId);
            SaveRegistry();
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

    private void LoadRegistry()
    {
        if (!File.Exists(_registryFilePath)) return;

        try
        {
            var json = File.ReadAllText(_registryFilePath);
            var plugins = JsonSerializer.Deserialize<List<PluginInfo>>(json);
            if (plugins != null)
            {
                foreach (var plugin in plugins)
                {
                    if (plugin.State == PluginState.Loaded)
                    {
                        plugin.State = PluginState.Installed;
                    }
                    _pluginRegistry[plugin.PluginId] = plugin;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load plugin registry: {ex.Message}");
        }
    }

    private void SaveRegistry()
    {
        try
        {
            var json = JsonSerializer.Serialize(_pluginRegistry.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_registryFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save plugin registry: {ex.Message}");
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
}
