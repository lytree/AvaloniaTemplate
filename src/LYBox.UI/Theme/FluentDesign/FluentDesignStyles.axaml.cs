using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace LYBox.UI.Theme.FluentDesign;

public class FluentDesignStyles : Styles
{
    public FluentDesignStyles()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
