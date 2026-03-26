using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Ursa.Controls;
using Avalonia.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.UI.Views;

public partial class MainSplashWindow : SplashWindow
{
    public MainSplashWindow()
    {
        InitializeComponent();
    }

    protected override async Task<Window?> CreateNextWindow()
    {
        var navigationService = App.ServiceProvider?.GetRequiredService<Avalonia.UI.Services.INavigationService>();
        var menuConfigurationService = App.ServiceProvider?.GetRequiredService<Avalonia.UI.Services.IMenuConfigurationService>();
        return new MainWindow()
        {
            DataContext = new MainViewViewModel(navigationService!, menuConfigurationService!)
        };
    }
}
