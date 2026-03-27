using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

public partial class TimeRangePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private TimeSpan? _startTime;
    [ObservableProperty] private TimeSpan? _endTime;

    public TimeRangePickerDemoViewModel()
    {
        StartTime = new TimeSpan(8, 21, 0);
        EndTime = new TimeSpan(18, 22, 0);
    }
}





