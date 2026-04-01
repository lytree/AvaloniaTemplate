using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Avalonia.Plugin.Shared;

public class ViewLocator : IDataTemplate
{
    // 核心注册表：ViewModel Type -> View Factory (O(1) 查找)
    private static readonly Dictionary<Type, ViewFactory> _viewRegistry = new(100);
    /// <summary>
    /// 手动注册接口：支持外部或插件自定义视图映射
    /// </summary>
    public static void Register<TViewModel, TView>()
        where TView : Control, new()
    {
        _viewRegistry[typeof(TViewModel)] = () => new TView();
    }

    /// <summary>
    /// 批量注册接口：供插件初始化时使用
    /// </summary>
    public static void RegisterRange(IEnumerable<KeyValuePair<Type, ViewFactory>> definitions)
    {
        foreach (var def in definitions)
        {
            _viewRegistry[def.Key] = def.Value;
        }
    }
    /// <summary>
    /// 插件加载器在启动时调用，注入插件定义的视图映射
    /// </summary>
    public static void RegisterPlugin(IPlugin plugin)
    {
        var definitions = plugin.GetViewDefinitions();
        if (definitions == null) return;

        foreach (var def in definitions)
        {
            // 如果存在冲突，新插件映射会覆盖旧映射
            _viewRegistry[def.Key] = def.Value;
        }
    }

    /// <summary>
    /// Avalonia 框架调用的构建方法
    /// </summary>
    public Control? Build(object? data)
    {
        if (data is null) return null;

        var type = data.GetType();

        // 极速路径：直接从字典检索工厂委托
        if (_viewRegistry.TryGetValue(type, out var factory))
        {
            var control = factory();
            control.DataContext = data;
            return control;
        }

        // 备选方案：如果未注册，显示一个友好的提示框
        return new TextBlock
        {
            Text = $"View not found for: {type.FullName}. \nPlease ensure it is registered in IPlugin.GetViewDefinitions().",
            VerticalAlignment = Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
    }

    public bool Match(object? data) => data is not null;
}
