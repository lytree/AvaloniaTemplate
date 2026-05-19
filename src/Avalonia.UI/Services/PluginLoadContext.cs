using System.Reflection;
using System.Runtime.Loader;

namespace Avalonia.UI.Services;

internal class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] ExcludedPrefixes =
    [
        "System.",
        "Microsoft.",
        "Avalonia.",
        "CommunityToolkit.",
        "Irihi.",
        "SQLitePCLRaw.",
    ];

    private static readonly HashSet<string> ExcludedExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Avalonia",
        "Ursa",
        "Semi.Avalonia",
        "Microsoft.Data.Sqlite",
        "MicroCom.Runtime",
    };

    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _pluginDirectory = Path.GetDirectoryName(pluginPath) ?? pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;

        if (IsExcluded(name))
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        assemblyPath = ProbePluginDirectory(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    private static bool IsExcluded(string name)
    {
        if (ExcludedExactNames.Contains(name))
            return true;

        foreach (var prefix in ExcludedPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string? ProbePluginDirectory(AssemblyName assemblyName)
    {
        var dllName = $"{assemblyName.Name}.dll";

        foreach (var dllPath in Directory.GetFiles(_pluginDirectory, dllName, SearchOption.AllDirectories))
        {
            try
            {
                var foundName = AssemblyName.GetAssemblyName(dllPath);
                if (string.Equals(foundName.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return dllPath;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
