using Avalonia.Plugin.Shared;
using Avalonia.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainViewViewModel MainViewViewModel { get; set; }

    public MainWindowViewModel()
    {
        var navigationService = App.ServiceProvider?.GetRequiredService<INavigationService>();
        var menuConfigurationService = App.ServiceProvider?.GetRequiredService<IMenuConfigurationService>();
        MainViewViewModel = new MainViewViewModel(navigationService!, menuConfigurationService!);
    }
}
