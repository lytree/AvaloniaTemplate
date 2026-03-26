using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Converters;

public class MenuDataTemplateSelector: IDataTemplate
{
    public IDataTemplate? MenuTemplate { get; set; }
    public IDataTemplate? SeparatorTemplate { get; set; }
    
    public Control? Build(object? param)
    {
        if (param is MenuItemViewModel vm)
        {
            if (vm.IsSeparator) return SeparatorTemplate?.Build(vm);
            else return MenuTemplate?.Build(vm);
        }
        return null;
    }

    public bool Match(object? data)
    {
        return true;
    }
}
