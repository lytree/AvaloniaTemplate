using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Avalonia.Plugin.Shared.Converters;

public class ViewLocator: IDataTemplate
{
    private static readonly List<string> _searchNamespaces = new()
    {
        "Avalonia.UI.Pages",
        "Avalonia.UI.Views"
    };

    public static void AddSearchNamespace(string @namespace)
    {
        if (!_searchNamespaces.Contains(@namespace))
        {
            _searchNamespaces.Add(@namespace);
        }
    }

    public Control? Build(object? param)
    {
        if (param is null) return null;
        var name = param.GetType().Name.Replace("ViewModel", "");
        
        // 尝试在所有搜索命名空间中查找
        foreach (var ns in _searchNamespaces)
        {
            var type = Type.GetType($"{ns}.{name}");
            if (type != null)
            {
                try
                {
                    return (Control)Activator.CreateInstance(type)!;
                }
                catch (Exception ex)
                {
                    return new TextBlock { Text = $"Error creating {name}: {ex.Message}" };
                }
            }
        }
        
        // 尝试从ViewModel所在的命名空间查找
        var viewModelType = param.GetType();
        var viewModelNamespace = viewModelType.Namespace;
        if (!string.IsNullOrEmpty(viewModelNamespace))
        {
            // 将ViewModel命名空间中的ViewModels替换为Pages
            var viewNamespace = viewModelNamespace.Replace("ViewModels", "Pages");
            var type = Type.GetType($"{viewNamespace}.{name}");
            if (type != null)
            {
                try
                {
                    return (Control)Activator.CreateInstance(type)!;
                }
                catch (Exception ex)
                {
                    return new TextBlock { Text = $"Error creating {name}: {ex.Message}" };
                }
            }
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return true;
    }
}
