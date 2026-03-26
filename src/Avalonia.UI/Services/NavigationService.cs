using System.Collections.Generic;
using Avalonia.UI.ViewModels;

namespace Avalonia.UI.Services;

public class NavigationService : INavigationService
{
    private readonly Dictionary<string, ViewModelFactory> _viewModelFactories = new();

    public NavigationService()
    {
        // 注册默认导航项
        RegisterDefaultNavigations();
    }

    private void RegisterDefaultNavigations()
    {
        // 介绍和关于
        RegisterNavigation(MenuKeys.MenuKeyIntroduction, () => new IntroductionDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyAboutUs, () => new AboutUsDemoViewModel());
        
        // 按钮和输入
        RegisterNavigation(MenuKeys.MenuKeyButtonGroup, () => new ButtonGroupDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyIconButton, () => new IconButtonDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyAutoCompleteBox, () => new AutoCompleteBoxDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyClassInput, () => new ClassInputDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyEnumSelector, () => new EnumSelectorDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyForm, () => new FormDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyKeyGestureInput, () => new KeyGestureInputDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyIpBox, () => new IPv4BoxDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyMultiComboBox, () => new MultiComboBoxDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyMultiAutoCompleteBox, () => new MultiAutoCompleteBoxDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyNumericUpDown, () => new NumericUpDownDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyNumPad, () => new NumPadDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyPathPicker, () => new PathPickerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyPinCode, () => new PinCodeDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyRangeSlider, () => new RangeSliderDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyRating, () => new RatingDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeySelectionList, () => new SelectionListDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyTagInput, () => new TagInputDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyThemeToggler, () => new ThemeTogglerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyTreeComboBox, () => new TreeComboBoxDemoViewModel());
        
        // 对话框和反馈
        RegisterNavigation(MenuKeys.MenuKeyDialog, () => new DialogDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyDrawer, () => new DrawerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyLoading, () => new LoadingDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyMessageBox, () => new MessageBoxDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyNotification, () => new NotificationDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyPopConfirm, () => new PopConfirmDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyToast, () => new ToastDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeySkeleton, () => new SkeletonDemoViewModel());
        
        // 日期和时间
        RegisterNavigation(MenuKeys.MenuKeyDatePicker, () => new DatePickerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyDateRangePicker, () => new DateRangePickerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyDateTimePicker, () => new DateTimePickerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyTimeBox, () => new TimeBoxDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyTimePicker, () => new TimePickerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyTimeRangePicker, () => new TimeRangePickerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyClock, () => new ClockDemoViewModel());
        
        // 导航和菜单
        RegisterNavigation(MenuKeys.MenuKeyAnchor, () => new AnchorDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyBreadcrumb, () => new BreadcrumbDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyNavMenu, () => new NavMenuDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyPagination, () => new PaginationDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyToolBar, () => new ToolBarDemoViewModel());
        
        // 布局和显示
        RegisterNavigation(MenuKeys.MenuKeyAspectRatioLayout, () => new AspectRatioLayoutDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyAvatar, () => new AvatarDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyBadge, () => new BadgeDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyBanner, () => new BannerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyDescriptions, () => new DescriptionsDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyDisableContainer, () => new DisableContainerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyDivider, () => new DividerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyDualBadge, () => new DualBadgeDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyImageViewer, () => new ImageViewerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyElasticWrapPanel, () => new ElasticWrapPanelDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyMarquee, () => new MarqueeDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyNumberDisplayer, () => new NumberDisplayerDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyQrCode, () => new QrCodeDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyScrollToButton, () => new ScrollToButtonDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyTimeline, () => new TimelineDemoViewModel());
        RegisterNavigation(MenuKeys.MenuKeyTwoTonePathIcon, () => new TwoTonePathIconDemoViewModel());
        
        // 设置
        RegisterNavigation(MenuKeys.MenuKeySettings, () => new SettingsPageViewModel());
    }

    public void RegisterNavigation(string key, ViewModelFactory factory)
    {
        _viewModelFactories[key] = factory;
    }

    public void RegisterNavigations(Dictionary<string, ViewModelFactory> navigations)
    {
        foreach (var (key, factory) in navigations)
        {
            _viewModelFactories[key] = factory;
        }
    }

    public object CreateViewModel(string key)
    {
        if (_viewModelFactories.TryGetValue(key, out var factory))
        {
            return factory();
        }
        throw new System.ArgumentOutOfRangeException(nameof(key), key, null);
    }

    public IEnumerable<string> GetNavigationKeys()
    {
        return _viewModelFactories.Keys;
    }
}
