using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Ursa.Controls;
using Avalonia.Plugin.Shared;
using Avalonia.UI.ViewModels;
using Avalonia.UI.Services;

namespace Avalonia.UI.Views;

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
