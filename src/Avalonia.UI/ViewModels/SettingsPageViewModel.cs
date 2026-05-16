using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Models;
using Avalonia.Plugin.Shared.Services;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.UI.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly ISettingsService? _settingsService;

    public ObservableCollection<SettingsGroupViewModel> Groups { get; } = [];

    public SettingsPageViewModel()
    {
    }

    public SettingsPageViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    [RelayCommand]
    public void Refresh()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (_settingsService == null) return;

        Groups.Clear();
        var allSettings = _settingsService.GetAllSettings();
        var grouped = allSettings.GroupBy(s => s.GroupName);

        foreach (var group in grouped.OrderBy(g => g.Min(s => s.GroupOrder)))
        {
            var groupVm = new SettingsGroupViewModel(group.Key, _settingsService);
            foreach (var setting in group.OrderBy(s => s.ItemOrder))
            {
                groupVm.Items.Add(new SettingEntryViewModel(setting, _settingsService));
            }
            Groups.Add(groupVm);
        }
    }
}

public class SettingsGroupViewModel : ViewModelBase
{
    public string GroupName { get; }
    public ObservableCollection<SettingEntryViewModel> Items { get; } = [];
    private readonly ISettingsService _settingsService;

    public SettingsGroupViewModel(string groupName, ISettingsService settingsService)
    {
        GroupName = groupName;
        _settingsService = settingsService;
    }
}

public partial class SettingEntryViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly SettingItem _setting;

    public string Key => _setting.Key;
    public string DisplayName => _setting.DisplayName;
    public string? Description => _setting.Description;
    public SettingType SettingType => _setting.SettingType;
    public List<string> Options => _setting.GetOptions();

    [ObservableProperty] private string _textValue;
    [ObservableProperty] private bool _switchValue;
    [ObservableProperty] private string? _dropdownValue;
    public ObservableCollection<string> DropdownOptions { get; }

    public bool IsText => SettingType == SettingType.Text;
    public bool IsSwitch => SettingType == SettingType.Switch;
    public bool IsDropdown => SettingType == SettingType.Dropdown;

    public SettingEntryViewModel(SettingItem setting, ISettingsService settingsService)
    {
        _setting = setting;
        _settingsService = settingsService;

        _textValue = setting.RawValue;
        _switchValue = setting.GetValue<bool>();
        _dropdownValue = setting.RawValue;
        DropdownOptions = new ObservableCollection<string>(setting.GetOptions());
    }

    partial void OnTextValueChanged(string value)
    {
        _settingsService.SetValue(Key, value);
    }

    partial void OnSwitchValueChanged(bool value)
    {
        _settingsService.SetValue(Key, value);

        if (Key == "App.SidebarCollapsed")
        {
            var app = Application.Current;
        }
    }

    partial void OnDropdownValueChanged(string? value)
    {
        if (value == null) return;
        _settingsService.SetValue(Key, value);

        if (Key == "App.Theme")
        {
            var app = Application.Current;
            if (app is not null)
            {
                app.RequestedThemeVariant = value switch
                {
                    "Light" => ThemeVariant.Light,
                    "Dark" => ThemeVariant.Dark,
                    _ => ThemeVariant.Default
                };
            }
        }
    }
}
