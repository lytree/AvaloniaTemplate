using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Avalonia.Plugin.Shared;

public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, ViewFactory> _viewRegistry = new(100);
    private static readonly Dictionary<object, Control> _viewCache = new(100);

    public static void Register<TViewModel, TView>()
        where TView : Control, new()
    {
        _viewRegistry[typeof(TViewModel)] = () => new TView();
    }

    public static void RegisterRange(IEnumerable<KeyValuePair<Type, ViewFactory>> definitions)
    {
        foreach (var def in definitions)
        {
            _viewRegistry[def.Key] = def.Value;
        }
    }

    public static void RegisterPlugin(IPlugin plugin)
    {
        var definitions = plugin.GetViewDefinitions();
        if (definitions == null) return;

        foreach (var def in definitions)
        {
            _viewRegistry[def.Key] = def.Value;
        }
    }

    public static void InvalidateViewCache(object viewModel)
    {
        _viewCache.Remove(viewModel);
    }

    public static void InvalidateAllViewCache()
    {
        _viewCache.Clear();
    }

    public Control? Build(object? data)
    {
        if (data is null) return null;

        if (_viewCache.TryGetValue(data, out var cachedControl))
        {
            return cachedControl;
        }

        var type = data.GetType();

        if (_viewRegistry.TryGetValue(type, out var factory))
        {
            var control = factory();
            control.DataContext = data;
            _viewCache[data] = control;
            return control;
        }

        return new TextBlock
        {
            Text = $"View not found for: {type.FullName}. \nPlease ensure it is registered in IPlugin.GetViewDefinitions().",
            VerticalAlignment = Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
    }

    public bool Match(object? data) => data is not null;
}
