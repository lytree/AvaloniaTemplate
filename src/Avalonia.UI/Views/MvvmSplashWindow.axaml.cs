using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Ursa.Controls;
using Avalonia.UI.ViewModels;

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
            return new MainWindow()
            {
                DataContext = new MainViewViewModel()
            };
        }
        return null;
    }
}
