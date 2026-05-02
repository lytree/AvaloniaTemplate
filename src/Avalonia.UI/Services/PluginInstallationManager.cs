using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;

namespace Avalonia.UI.Services;

public class PluginInstallationManager : IPluginInstallationManager
{
    private readonly PluginLoader _pluginLoader;
    private readonly string _pluginsDirectory;

    public event EventHandler<PluginInfo>? PluginInstalled;
    public event EventHandler<PluginInfo>? PluginUninstalled;

    public PluginInstallationManager(PluginLoader pluginLoader, string? pluginsDirectory = null)
    {
        _pluginLoader = pluginLoader;
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        Directory.CreateDirectory(_pluginsDirectory);
    }

    public string GetPluginInstallDirectory() => _pluginsDirectory;

    public string GetPluginDirectory(string pluginId) => Path.Combine(_pluginsDirectory, pluginId);

    public async Task<PluginInstallResult> InstallFromFileAsync(string packageFilePath, IProgress<double>? progress = null)
    {
        if (!File.Exists(packageFilePath))
        {
            return new PluginInstallResult { Success = false, ErrorMessage = "Package file not found" };
        }

        await using var stream = File.OpenRead(packageFilePath);
        return await InstallFromStreamAsync(stream, Path.GetFileName(packageFilePath), progress);
    }

    public async Task<PluginInstallResult> InstallFromStreamAsync(Stream stream, string fileName, IProgress<double>? progress = null)
    {
        PluginInfo? pluginInfo = null;
        string? tempDir = null;

        try
        {
            tempDir = Path.Combine(Path.GetTempPath(), $"plugin_install_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var entries = archive.Entries;
                var totalEntries = entries.Count;
                var processed = 0;

                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));

                    if (!destinationPath.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
                    {
                        return new PluginInstallResult
                        {
                            Success = false,
                            ErrorMessage = "Security: Path traversal detected in package"
                        };
                    }

                    var dir = Path.GetDirectoryName(destinationPath);
                    if (dir != null) Directory.CreateDirectory(dir);

                    entry.ExtractToFile(destinationPath, overwrite: true);

                    processed++;
                    progress?.Report((double)processed / totalEntries);
                }
            }

            pluginInfo = await ParsePluginMetadataAsync(tempDir);

            if (pluginInfo == null)
            {
                return new PluginInstallResult
                {
                    Success = false,
                    ErrorMessage = "Invalid plugin package: no valid metadata found"
                };
            }

            var existingPlugin = _pluginLoader.GetPlugin(pluginInfo.PluginId);
            if (existingPlugin != null)
            {
                var targetDir = GetPluginDirectory(pluginInfo.PluginId);
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
            }

            var installDir = GetPluginDirectory(pluginInfo.PluginId);
            Directory.CreateDirectory(installDir);

            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempDir, file);
                var destPath = Path.GetFullPath(Path.Combine(installDir, relativePath));

                if (!destPath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        ErrorMessage = "Security: Path traversal detected during installation"
                    };
                }

                var destDir = Path.GetDirectoryName(destPath);
                if (destDir != null) Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }

            var mainAssembly = Directory.GetFiles(installDir, $"{pluginInfo.Name}.dll", SearchOption.AllDirectories)
                .FirstOrDefault()
                ?? Directory.GetFiles(installDir, "*.dll", SearchOption.AllDirectories)
                    .FirstOrDefault(f => !f.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase));

            pluginInfo.InstallPath = installDir;
            pluginInfo.AssemblyPath = mainAssembly ?? string.Empty;
            pluginInfo.State = PluginState.Installed;
            pluginInfo.InstallTime = DateTime.UtcNow;

            _pluginLoader.RegisterPlugin(pluginInfo);

            PluginInstalled?.Invoke(this, pluginInfo);

            return new PluginInstallResult { Success = true, PluginInfo = pluginInfo };
        }
        catch (Exception ex)
        {
            return new PluginInstallResult { Success = false, ErrorMessage = $"Installation failed: {ex.Message}" };
        }
        finally
        {
            if (tempDir != null && Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public Task<bool> UninstallAsync(string pluginId)
    {
        var pluginInfo = _pluginLoader.GetPlugin(pluginId);
        if (pluginInfo == null) return Task.FromResult(false);

        if (pluginInfo.IsBuiltIn) return Task.FromResult(false);

        _pluginLoader.UnregisterPlugin(pluginId);

        var installDir = GetPluginDirectory(pluginId);
        if (Directory.Exists(installDir))
        {
            try
            {
                Directory.Delete(installDir, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete plugin directory: {ex.Message}");
            }
        }

        PluginUninstalled?.Invoke(this, pluginInfo);
        return Task.FromResult(true);
    }

    public Task<bool> EnablePluginAsync(string pluginId)
    {
        var pluginInfo = _pluginLoader.GetPlugin(pluginId);
        if (pluginInfo == null) return Task.FromResult(false);

        var result = _pluginLoader.LoadPlugin(pluginInfo);
        return Task.FromResult(result.Success);
    }

    public Task<bool> DisablePluginAsync(string pluginId)
    {
        var pluginInfo = _pluginLoader.GetPlugin(pluginId);
        if (pluginInfo == null) return Task.FromResult(false);

        _pluginLoader.UnloadPlugin(pluginId);
        return Task.FromResult(true);
    }

    private async Task<PluginInfo?> ParsePluginMetadataAsync(string directory)
    {
        var nuspecFiles = Directory.GetFiles(directory, "*.nuspec", SearchOption.AllDirectories);
        if (nuspecFiles.Length > 0)
        {
            return ParseNuspecMetadata(nuspecFiles[0]);
        }

        var manifestFile = Path.Combine(directory, "plugin.json");
        if (File.Exists(manifestFile))
        {
            var json = await File.ReadAllTextAsync(manifestFile);
            return JsonSerializer.Deserialize<PluginInfo>(json);
        }

        var dllFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles)
        {
            try
            {
                var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(dll);
                return new PluginInfo
                {
                    PluginId = Guid.NewGuid().ToString("N"),
                    Name = assemblyName.Name ?? "Unknown",
                    Version = assemblyName.Version?.ToString() ?? "1.0.0",
                    Author = "Unknown",
                    Description = string.Empty
                };
            }
            catch
            {
            }
        }

        return null;
    }

    private PluginInfo? ParseNuspecMetadata(string nuspecPath)
    {
        try
        {
            var doc = XDocument.Load(nuspecPath);
            XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
            var metadata = doc.Descendants(ns + "metadata").FirstOrDefault()
                ?? doc.Descendants("metadata").FirstOrDefault();

            if (metadata == null) return null;

            var id = metadata.Element(ns + "id")?.Value ?? metadata.Element("id")?.Value ?? Guid.NewGuid().ToString("N");
            var version = metadata.Element(ns + "version")?.Value ?? metadata.Element("version")?.Value ?? "1.0.0";
            var title = metadata.Element(ns + "title")?.Value ?? metadata.Element("title")?.Value ?? id;
            var authors = metadata.Element(ns + "authors")?.Value ?? metadata.Element("authors")?.Value ?? "Unknown";
            var description = metadata.Element(ns + "description")?.Value ?? metadata.Element("description")?.Value ?? string.Empty;

            var dependencies = new List<string>();
            var depsGroup = metadata.Element(ns + "dependencies") ?? metadata.Element("dependencies");
            if (depsGroup != null)
            {
                foreach (var dep in depsGroup.Elements(ns + "dependency").Concat(depsGroup.Elements("dependency")))
                {
                    var depId = dep.Attribute("id")?.Value;
                    if (depId != null) dependencies.Add(depId);
                }
            }

            return new PluginInfo
            {
                PluginId = id,
                Name = title,
                Version = version,
                Author = authors,
                Description = description,
                Dependencies = dependencies
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to parse nuspec: {ex.Message}");
            return null;
        }
    }
}
