using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Ursa.Controls;
using Avalonia.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.UI.Views;

public partial class MvvmSplashWindow : SplashWindow
{
    public MvvmSplashWindow()
    {
        InitializeComponent();
    }

    protected override async Task<Window?> CreateNextWindow()
    {
        if (this.DialogResult is true)
        {
            var navigationService = App.ServiceProvider?.GetRequiredService<Avalonia.UI.Services.INavigationService>();
            var menuConfigurationService = App.ServiceProvider?.GetRequiredService<Avalonia.UI.Services.IMenuConfigurationService>();
            return new MainWindow()
            {
                DataContext = new MainViewViewModel(navigationService!, menuConfigurationService!)
            };
        }
        return null;
    }
}
