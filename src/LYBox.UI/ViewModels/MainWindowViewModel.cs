using LYBox.Plugin.Shared;
using LYBox.UI.Services;

namespace LYBox.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainViewViewModel MainViewViewModel { get; set; }

    public MainWindowViewModel()
    {
        var navigationService = ServiceLocator.GetService<INavigationService>();
        var menuConfigurationService = ServiceLocator.GetService<IMenuConfigurationService>();
        MainViewViewModel = new MainViewViewModel(navigationService!, menuConfigurationService!);
    }
}
