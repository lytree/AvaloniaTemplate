using System.Globalization;
using System.Resources;

namespace Avalonia.Plugin.Shared.Services;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }

    string GetString(string key);

    string GetString(string key, string fallback);

    void SetCulture(CultureInfo culture);

    void RegisterResourceManager(ResourceManager manager, string? prefix = null);

    event EventHandler<CultureInfo>? CultureChanged;
}
