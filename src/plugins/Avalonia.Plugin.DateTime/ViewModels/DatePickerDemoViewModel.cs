using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Plugin.DateTimeControls.ViewModels;

public partial class DatePickerDemoViewModel: ObservableObject
{
    [ObservableProperty] private DateTime? _selectedDate;

    public DatePickerDemoViewModel()
    {
        SelectedDate = DateTime.Today;
    }
}
