
using Avalonia.Controls;
using Avalonia.Plugin.Shared.ViewModels;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.Shared;


public interface IPlugin
{
    /// <summary>
    /// 【新增】获取插件提供的 ViewModel 与 View 的映射关系
    /// </summary>
    /// <returns>Key 为 ViewModel 类型，Value 为创建对应 View 的工厂方法</returns>
    IEnumerable<KeyValuePair<Type, ViewFactory>> GetViewDefinitions();
    /// <summary>
    /// 获取插件提供的导航项
    /// </summary>
    /// <returns>导航项字典，键为导航键，值为 ViewModel 工厂方法</returns>
    Dictionary<string, ViewModelFactory> GetNavigationItems();
    /// <summary>
    /// 获取插件提供的菜单项
    /// </summary>
    /// <returns>菜单项列表，包含菜单项和其父菜单项键（可选）</returns>
    List<KeyValuePair<string, MenuItemViewModel>> GetMenuItems();
}


/// <summary>
/// ViewModel 工厂委托
/// </summary>
public delegate object ViewModelFactory();
/// <summary>
/// 视图工厂委托
/// </summary>
public delegate Control ViewFactory();



/// <summary>
/// 表单元素接口
/// </summary>
public interface IFormElement
{
}

/// <summary>
/// 表单组视图模型接口
/// </summary>
public interface IFormGroupViewModel : IFormElement
{
    string? Title { get; set; }
    ObservableCollection<IFromItemViewModel> Items { get; set; }
}

/// <summary>
/// 表单项视图模型接口
/// </summary>
public interface IFromItemViewModel : IFormElement
{
    string? Label { get; set; }
}

/// <summary>
/// 工具栏项视图模型
/// </summary>
public class ToolBarItemViewModel
{
    public string Content { get; set; }
    public object Command { get; set; }
    public object OverflowMode { get; set; }
}

/// <summary>
/// 工具栏分隔符视图模型
/// </summary>
public class ToolBarSeparatorViewModel : ToolBarItemViewModel
{
}

/// <summary>
/// 工具栏按钮项视图模型
/// </summary>
public class ToolBarButtonItemViewModel : ToolBarItemViewModel
{
}

/// <summary>
/// 工具栏复选框项视图模型
/// </summary>
public class ToolBarCheckBoxItemViweModel : ToolBarItemViewModel
{
    public bool IsChecked { get; set; }
}

/// <summary>
/// 工具栏组合框项视图模型
/// </summary>
public class ToolBarComboBoxItemViewModel : ToolBarItemViewModel
{
    public object SelectedItem { get; set; }
    public object Items { get; set; }
}
