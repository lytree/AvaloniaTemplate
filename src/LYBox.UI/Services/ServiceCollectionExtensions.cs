using LYBox.Plugin.Shared.Services;
using LYBox.UI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace LYBox.UI.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaServices(this IServiceCollection services)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs");

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Microsoft", LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

            builder.AddZLoggerConsole();
            builder.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (dt, seq) =>
                    Path.Combine(logPath, $"app-{dt:yyyy-MM-dd}_{seq:000}.log");
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB = 10240; // 10MB
            });
        });

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IMenuConfigurationService, MenuConfigurationService>();

        services.AddSingleton<PluginLoader>();
        services.AddSingleton<IPluginLoader>(sp => sp.GetRequiredService<PluginLoader>());

        services.AddSingleton<IPluginInstallationManager, PluginInstallationManager>();

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "appdata.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<IWindowInfoService, WindowInfoService>();

        services.AddLocalization();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<ITaskRegistry, TaskRegistry>();

        return services;
    }
}
