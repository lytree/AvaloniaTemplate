using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

public partial class TimePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private TimeSpan? _time;
    
    public TimePickerDemoViewModel()
    {
        Time = new TimeSpan(12, 20, 0);
    }
}





