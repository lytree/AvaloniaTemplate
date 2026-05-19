using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Styling;
using Avalonia.UI.Theme.Animations;

namespace Avalonia.UI.Theme;

public partial class UrsaSemiTheme : Styles
{
    public UrsaSemiTheme(IServiceProvider? provider = null)
    {
        AvaloniaXamlLoader.Load(provider, this);
        Resources.MergedDictionaries.Add(new DefaultSizeAnimations());
        Resources.MergedDictionaries.Add(new NavMenuSizeAnimations());
    }

    public CultureInfo? Locale
    {
        get;
        set
        {
            try
            {
                field = value ?? new CultureInfo("zh-CN");
                SyncLocalizationService(field!);
            }
            catch
            {
                field = CultureInfo.InvariantCulture;
            }
        }
    }

    private static void SyncLocalizationService(CultureInfo culture)
    {
        try
        {
            if (ServiceLocator.TryGetService<ILocalizationService>(out var service) && service is not null)
            {
                service.SetCulture(culture);
            }
        }
        catch
        {
        }
    }

    public static void OverrideLocaleResources(Application application, CultureInfo? culture)
    {
        if (culture is null) return;
        try
        {
            if (ServiceLocator.TryGetService<ILocalizationService>(out var service) && service is not null)
            {
                service.SetCulture(culture);
            }
        }
        catch
        {
        }
    }

    public static void OverrideLocaleResources(StyledElement element, CultureInfo? culture)
    {
        if (culture is null) return;
        try
        {
            if (ServiceLocator.TryGetService<ILocalizationService>(out var service) && service is not null)
            {
                service.SetCulture(culture);
            }
        }
        catch
        {
        }
    }
}
