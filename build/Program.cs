using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public string BuildConfiguration { get; }
    public string PackageVersion { get; }
    public string NuGetSource { get; }
    public string NuGetApiKey { get; }
    public string RuntimeIdentifier { get; }
    public bool SelfContained { get; }

    public string RootDir { get; }
    public string PackagesDir { get; }
    public string NuGetPackagesDir { get; }
    public string BinPackagesDir { get; }
    public string PluginPackagesDir { get; }

    public string GeneratorsProject { get; }
    public string SharedProject { get; }
    public string LauncherProject { get; }
    public IReadOnlyList<string> PluginProjects { get; }

    public DotNetMSBuildSettings CreateMSBuildSettings()
    {
        return new DotNetMSBuildSettings()
            .SetVersion(PackageVersion)
            .SetConfiguration(BuildConfiguration)
            .WithProperty("ContinuousIntegrationBuild", "true");
    }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        PackageVersion = context.Argument("package-version", "1.0.0");
        NuGetSource = context.Argument("nuget-source", "https://api.nuget.org/v3/index.json");
        NuGetApiKey = context.Argument("nuget-api-key", "");
        RuntimeIdentifier = context.Argument("runtime-identifier", "");
        SelfContained = context.Argument("self-contained", false);

        RootDir = context.Environment.WorkingDirectory.FullPath;
        PackagesDir = Path.Combine(RootDir, "packages");
        NuGetPackagesDir = Path.Combine(PackagesDir, "nuget");
        BinPackagesDir = Path.Combine(PackagesDir, "bin");
        PluginPackagesDir = Path.Combine(PackagesDir, "plugins");

        GeneratorsProject = Path.Combine(RootDir, "src", "Avalonia.Plugin.Generators", "Avalonia.Plugin.Generators.csproj");
        SharedProject = Path.Combine(RootDir, "src", "Avalonia.Plugin.Shared", "Avalonia.Plugin.Shared.csproj");
        LauncherProject = Path.Combine(RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "Avalonia.Launcher.Desktop.csproj");

        PluginProjects = new List<string>
        {
            Path.Combine(RootDir, "plugins", "Avalonia.Plugin.ButtonsInputs", "Avalonia.Plugin.ButtonsInputs.csproj"),
            Path.Combine(RootDir, "plugins", "Avalonia.Plugin.DateTime", "Avalonia.Plugin.DateTime.csproj"),
            Path.Combine(RootDir, "plugins", "Avalonia.Plugin.DialogFeedbacks", "Avalonia.Plugin.DialogFeedbacks.csproj"),
            Path.Combine(RootDir, "plugins", "Avalonia.Plugin.LayoutDisplay", "Avalonia.Plugin.LayoutDisplay.csproj"),
            Path.Combine(RootDir, "plugins", "Avalonia.Plugin.NavigationMenus", "Avalonia.Plugin.NavigationMenus.csproj"),
            Path.Combine(RootDir, "plugins", "Avalonia.Plugin.TDLSharp", "Avalonia.Plugin.TDLSharp.csproj"),
        };
    }
}

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (Directory.Exists(context.PackagesDir))
        {
            context.CleanDirectory(context.PackagesDir);
        }

        var cleanDirs = new[]
        {
            Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Generators", "bin"),
            Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Generators", "obj"),
            Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Shared", "bin"),
            Path.Combine(context.RootDir, "src", "Avalonia.Plugin.Shared", "obj"),
            Path.Combine(context.RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "bin"),
            Path.Combine(context.RootDir, "src", "launcher", "Avalonia.Launcher.Desktop", "obj"),
        };

        foreach (var dir in cleanDirs)
        {
            if (Directory.Exists(dir))
            {
                context.CleanDirectory(dir);
            }
        }

        foreach (var plugin in context.PluginProjects)
        {
            var pluginDir = Path.GetDirectoryName(plugin)!;
            var binDir = Path.Combine(pluginDir, "bin");
            var objDir = Path.Combine(pluginDir, "obj");
            if (Directory.Exists(binDir)) context.CleanDirectory(binDir);
            if (Directory.Exists(objDir)) context.CleanDirectory(objDir);
        }

        context.Log.Information("Clean completed.");
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(CleanTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var msBuildSettings = context.CreateMSBuildSettings();

        context.DotNetBuild(context.GeneratorsProject, new DotNetBuildSettings
        {
            Configuration = context.BuildConfiguration,
            MSBuildSettings = msBuildSettings
        });

        context.DotNetBuild(context.SharedProject, new DotNetBuildSettings
        {
            Configuration = context.BuildConfiguration,
            MSBuildSettings = msBuildSettings
        });

        context.DotNetBuild(context.LauncherProject, new DotNetBuildSettings
        {
            Configuration = context.BuildConfiguration,
            MSBuildSettings = msBuildSettings
        });

        foreach (var plugin in context.PluginProjects)
        {
            context.DotNetBuild(plugin, new DotNetBuildSettings
            {
                Configuration = context.BuildConfiguration,
                MSBuildSettings = msBuildSettings
            });
        }

        context.Log.Information("Build completed.");
    }
}

[TaskName("PackNuGet")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackNuGetTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists(context.NuGetPackagesDir);

        var msBuildSettings = context.CreateMSBuildSettings();

        context.DotNetPack(context.GeneratorsProject, new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = msBuildSettings
        });

        context.DotNetPack(context.SharedProject, new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.NuGetPackagesDir,
            NoRestore = true,
            NoBuild = true,
            MSBuildSettings = msBuildSettings
        });

        context.Log.Information("NuGet packages created in: {0}", context.NuGetPackagesDir);
        foreach (var pkg in context.GetFiles(Path.Combine(context.NuGetPackagesDir, "*.nupkg")))
        {
            context.Log.Information("  {0}", pkg.GetFilename());
        }
    }
}

[TaskName("LocalInstall")]
[IsDependentOn(typeof(PackNuGetTask))]
public sealed class LocalInstallTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var localFeedName = "AvaloniaPluginLocal";
        var localFeedPath = context.NuGetPackagesDir;

        context.StartProcess("dotnet", new Cake.Core.IO.ProcessSettings
        {
            Arguments = $"nuget add source \"{localFeedPath}\" -n {localFeedName}"
        });

        context.Log.Information("Local NuGet feed '{0}' configured at: {1}", localFeedName, localFeedPath);
        context.Log.Information("To consume these packages, add the following to your nuget.config:");
        context.Log.Information("  <add key=\"{0}\" value=\"{1}\" />", localFeedName, localFeedPath);
    }
}

[TaskName("PushNuGet")]
[IsDependentOn(typeof(PackNuGetTask))]
public sealed class PushNuGetTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (string.IsNullOrEmpty(context.NuGetApiKey))
        {
            context.Log.Error("NuGet API key is required. Use --nuget-api-key=<KEY>");
            return;
        }

        var packages = context.GetFiles(Path.Combine(context.NuGetPackagesDir, "*.nupkg"));
        foreach (var pkg in packages)
        {
            context.Log.Information("Pushing {0}...", pkg.GetFilename());
            context.DotNetNuGetPush(pkg.FullPath, new DotNetNuGetPushSettings
            {
                Source = context.NuGetSource,
                ApiKey = context.NuGetApiKey
            });
        }

        context.Log.Information("NuGet packages pushed to: {0}", context.NuGetSource);
    }
}

[TaskName("PublishLauncher")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PublishLauncherTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists(context.BinPackagesDir);

        var settings = new DotNetPublishSettings
        {
            Configuration = context.BuildConfiguration,
            OutputDirectory = context.BinPackagesDir,
            NoRestore = true,
            NoBuild = true,
        };

        if (!string.IsNullOrEmpty(context.RuntimeIdentifier))
        {
            settings.Runtime = context.RuntimeIdentifier;
        }

        if (context.SelfContained)
        {
            settings.SelfContained = true;
        }

        context.DotNetPublish(context.LauncherProject, settings);

        context.Log.Information("Launcher published to: {0}", context.BinPackagesDir);
    }
}

[TaskName("PublishPlugins")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PublishPluginsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists(context.PluginPackagesDir);

        foreach (var plugin in context.PluginProjects)
        {
            var pluginName = Path.GetFileName(Path.GetDirectoryName(plugin))!;
            var pluginOutputDir = Path.Combine(context.PluginPackagesDir, pluginName);

            context.EnsureDirectoryExists(pluginOutputDir);

            context.DotNetPublish(plugin, new DotNetPublishSettings
            {
                Configuration = context.BuildConfiguration,
                OutputDirectory = pluginOutputDir,
                NoRestore = true,
                NoBuild = true,
            });

            context.Log.Information("Plugin published: {0} -> {1}", pluginName, pluginOutputDir);
        }

        context.Log.Information("All plugins published to: {0}", context.PluginPackagesDir);
    }
}

[TaskName("Pack")]
[IsDependentOn(typeof(PackNuGetTask))]
[IsDependentOn(typeof(PublishLauncherTask))]
[IsDependentOn(typeof(PublishPluginsTask))]
public class PackTask : FrostingTask
{
}

[TaskName("Default")]
[IsDependentOn(typeof(PackTask))]
public class DefaultTask : FrostingTask
{
}
