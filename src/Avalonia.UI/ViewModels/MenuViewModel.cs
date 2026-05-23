using System.Collections.ObjectModel;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.ViewModels;

namespace Avalonia.UI.ViewModels;

public class MenuViewModel : ViewModelBase
{
    public MenuViewModel()
    {
        MenuItems =
        [
            new() { MenuHeader = "NAV_Introduction", Key = "Introduction", IsSeparator = false },
        ];
    }

    public ObservableCollection<MenuItemViewModel> MenuItems { get; set; }

    public void RefreshHeaders()
    {
        foreach (var item in MenuItems)
            item.RefreshHeader();
    }
}
