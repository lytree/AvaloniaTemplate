using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.DateTimeControls.ViewModels;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.Plugin.DateTimeControls;

public class DateTimePlugin : IPlugin
{
    public string Name => "Date & Time Plugin";
    public string Version => "1.0.0";

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>
        {
            { MenuKeys.MenuKeyDatePicker, () => new DatePickerDemoViewModel() },
            { MenuKeys.MenuKeyDateRangePicker, () => new DateRangePickerDemoViewModel() },
            { MenuKeys.MenuKeyDateTimePicker, () => new DateTimePickerDemoViewModel() },
            { MenuKeys.MenuKeyTimeBox, () => new TimeBoxDemoViewModel() },
            { MenuKeys.MenuKeyTimePicker, () => new TimePickerDemoViewModel() },
            { MenuKeys.MenuKeyTimeRangePicker, () => new TimeRangePickerDemoViewModel() },
            { MenuKeys.MenuKeyClock, () => new ClockDemoViewModel() }
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var dateAndTime = new MenuItemViewModel
        {
            MenuHeader = "Date & Time",
            Children = new()
            {
                new() { MenuHeader = "Date Picker", Key = MenuKeys.MenuKeyDatePicker },
                new() { MenuHeader = "Date Range Picker", Key = MenuKeys.MenuKeyDateRangePicker },
                new() { MenuHeader = "Date Time Picker", Key = MenuKeys.MenuKeyDateTimePicker },
                new() { MenuHeader = "Time Box", Key = MenuKeys.MenuKeyTimeBox },
                new() { MenuHeader = "Time Picker", Key = MenuKeys.MenuKeyTimePicker },
                new() { MenuHeader = "Time Range Picker", Key = MenuKeys.MenuKeyTimeRangePicker },
                new() { MenuHeader = "Clock", Key = MenuKeys.MenuKeyClock }
            }
        };
        menuItems.Add(("Controls", dateAndTime));

        return menuItems;
    }
}



