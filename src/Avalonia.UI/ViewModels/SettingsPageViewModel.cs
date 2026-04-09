using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.UI.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    public ObservableCollection<ThemeItem> Themes { get; } = [
        new("Default", ThemeVariant.Default),
        new("Light", ThemeVariant.Light),
        new("Dark", ThemeVariant.Dark)
    ];

    [ObservableProperty] private ThemeItem? _selectedTheme;

    partial void OnSelectedThemeChanged(ThemeItem? oldValue, ThemeItem? newValue)
    {
        if (newValue is null) return;
        var app = Application.Current;
        if (app is not null)
        {
            app.RequestedThemeVariant = newValue.Theme;
        }
    }

    [ObservableProperty] private bool _isCollapsed;
}

public class ThemeItem(string name, ThemeVariant theme)
{
    public string Name { get; set; } = name;
    public ThemeVariant Theme { get; set; } = theme;
}