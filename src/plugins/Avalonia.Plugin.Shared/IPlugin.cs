using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.Shared;

public interface IPlugin
{
    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 初始化插件
    /// </summary>
    void Initialize();

    /// <summary>
    /// 获取插件提供的导航项
    /// </summary>
    /// <returns>导航项字典，键为导航键，值为 ViewModel 工厂方法</returns>
    Dictionary<string, ViewModelFactory> GetNavigationItems();

    /// <summary>
    /// 获取插件提供的菜单项
    /// </summary>
    /// <returns>菜单项列表，包含菜单项和其父菜单项键（可选）</returns>
    IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems();
}

/// <summary>
/// ViewModel 工厂委托
/// </summary>
public delegate object ViewModelFactory();

/// <summary>
/// 菜单项视图模型
/// </summary>
public class MenuItemViewModel
{
    public string MenuHeader { get; set; }
    public string Key { get; set; }
    public string Status { get; set; }
    public bool IsSeparator { get; set; }
    public ObservableCollection<MenuItemViewModel> Children { get; set; }
}

/// <summary>
/// 菜单键定义
/// </summary>
public static class MenuKeys
{
    public const string MenuKeyIntroduction = "Introduction";
    public const string MenuKeyAboutUs = "AboutUs";
    public const string MenuKeyAutoCompleteBox = "AutoCompleteBox";
    public const string MenuKeyAvatar = "Avatar";
    public const string MenuKeyBadge = "Badge";
    public const string MenuKeyBanner = "Banner";
    public const string MenuKeyBreadcrumb = "Breadcrumb";
    public const string MenuKeyButtonGroup = "ButtonGroup";
    public const string MenuKeyClassInput = "Class Input";
    public const string MenuKeyClock = "Clock";
    public const string MenuKeyDatePicker = "DatePicker";
    public const string MenuKeyDateRangePicker = "DateRangePicker";
    public const string MenuKeyDateTimePicker = "DateTimePicker";
    public const string MenuKeyDescriptions = "Descriptions";
    public const string MenuKeyDialog = "Dialog";
    public const string MenuKeyDisableContainer = "DisableContainer";
    public const string MenuKeyDivider = "Divider";
    public const string MenuKeyDrawer = "Drawer";
    public const string MenuKeyDualBadge = "DualBadge";
    public const string MenuKeyElasticWrapPanel = "ElasticWrapPanel";
    public const string MenuKeyEnumSelector = "EnumSelector";
    public const string MenuKeyForm = "Form";
    public const string MenuKeyIconButton = "IconButton";
    public const string MenuKeyImageViewer = "ImageViewer";
    public const string MenuKeyIpBox = "IPv4Box";
    public const string MenuKeyKeyGestureInput = "KeyGestureInput";
    public const string MenuKeyLoading = "Loading";
    public const string MenuKeyMarquee = "Marquee";
    public const string MenuKeyMessageBox = "MessageBox";
    public const string MenuKeyMultiComboBox = "MultiComboBox";
    public const string MenuKeyNavMenu = "NavMenu";
    public const string MenuKeyNotification = "Notification";
    public const string MenuKeyNumberDisplayer = "NumberDisplayer";
    public const string MenuKeyNumericUpDown = "NumericUpDown";
    public const string MenuKeyNumPad = "NumPad";
    public const string MenuKeyPagination = "Pagination";
    public const string MenuKeyPinCode = "PinCode";
    public const string MenuKeyPopConfirm = "PopConfirm";
    public const string MenuKeyQrCode = "QrCode";
    public const string MenuKeyRangeSlider = "RangeSlider";
    public const string MenuKeyRating = "Rating";
    public const string MenuKeyScrollToButton = "ScrollToButton";
    public const string MenuKeySelectionList = "SelectionList";
    public const string MenuKeySkeleton = "Skeleton";
    public const string MenuKeyTagInput = "TagInput";
    public const string MenuKeyThemeToggler = "ThemeToggler";
    public const string MenuKeyTimeBox = "TimeBox";
    public const string MenuKeyTimeline = "Timeline";
    public const string MenuKeyTimePicker = "TimePicker";
    public const string MenuKeyTimeRangePicker = "TimeRangePicker";
    public const string MenuKeyToast = "Toast";
    public const string MenuKeyToolBar = "ToolBar";
    public const string MenuKeyTreeComboBox = "TreeComboBox";
    public const string MenuKeyTwoTonePathIcon = "TwoTonePathIcon";
    public const string MenuKeyAspectRatioLayout = "AspectRatioLayout";
    public const string MenuKeyPathPicker = "PathPicker";
    public const string MenuKeyAnchor = "Anchor";
    public const string MenuKeyMultiAutoCompleteBox = "MultiAutoCompleteBox";
    public const string MenuKeySettings = "Settings";
}

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



