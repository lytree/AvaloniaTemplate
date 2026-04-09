using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.UI.ViewModels;

public partial class WebPageDemoViewModel : ObservableObject
{
    [ObservableProperty] private string _url = "https://avaloniaui.net";

    [ObservableProperty] private Uri _currentUri = new("https://avaloniaui.net");

    [RelayCommand]
    private void Navigate()
    {
        if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
        {
            CurrentUri = uri;
        }
    }
}
