using System.Threading.Tasks;
using Avalonia.Controls;
using Ursa.Controls;
using LYBox.Plugin.Shared;
using LYBox.UI.ViewModels;
using LYBox.UI.Services;

namespace LYBox.UI.Views;

public partial class MainSplashWindow : SplashWindow
{
    public MainSplashWindow()
    {
        InitializeComponent();
    }

    protected override async Task<Window?> CreateNextWindow()
    {
        var navigationService = ServiceLocator.GetService<INavigationService>();
        var menuConfigurationService = ServiceLocator.GetService<IMenuConfigurationService>();
        return new MainWindow()
        {
            DataContext = new MainViewViewModel(navigationService!, menuConfigurationService!)
        };
    }
}
