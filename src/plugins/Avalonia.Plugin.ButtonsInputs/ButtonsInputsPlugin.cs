using System.Collections.Generic;
using Avalonia.Plugin.Shared;
using Avalonia.Plugin.ButtonsInputs.ViewModels;

namespace Avalonia.Plugin.ButtonsInputs;

public class ButtonsInputsPlugin : IPlugin
{
    public string Name => "Buttons & Inputs Plugin";
    public string Version => "1.0.0";

    public void Initialize()
    {
        // 插件初始化逻辑
    }

    public Dictionary<string, ViewModelFactory> GetNavigationItems()
    {
        var navigationItems = new Dictionary<string, ViewModelFactory>
        {
            { MenuKeys.MenuKeyButtonGroup, () => new ButtonGroupDemoViewModel() },
            { MenuKeys.MenuKeyIconButton, () => new IconButtonDemoViewModel() },
            { MenuKeys.MenuKeyAutoCompleteBox, () => new AutoCompleteBoxDemoViewModel() },
            { MenuKeys.MenuKeyClassInput, () => new ClassInputDemoViewModel() },
            { MenuKeys.MenuKeyEnumSelector, () => new EnumSelectorDemoViewModel() },
            { MenuKeys.MenuKeyForm, () => new FormDemoViewModel() },
            { MenuKeys.MenuKeyKeyGestureInput, () => new KeyGestureInputDemoViewModel() },
            { MenuKeys.MenuKeyIpBox, () => new IPv4BoxDemoViewModel() },
            { MenuKeys.MenuKeyMultiComboBox, () => new MultiComboBoxDemoViewModel() },
            { MenuKeys.MenuKeyMultiAutoCompleteBox, () => new MultiAutoCompleteBoxDemoViewModel() },
            { MenuKeys.MenuKeyNumericUpDown, () => new NumericUpDownDemoViewModel() },
            { MenuKeys.MenuKeyNumPad, () => new NumPadDemoViewModel() },
            { MenuKeys.MenuKeyPathPicker, () => new PathPickerDemoViewModel() },
            { MenuKeys.MenuKeyPinCode, () => new PinCodeDemoViewModel() },
            { MenuKeys.MenuKeyRangeSlider, () => new RangeSliderDemoViewModel() },
            { MenuKeys.MenuKeyRating, () => new RatingDemoViewModel() },
            { MenuKeys.MenuKeySelectionList, () => new SelectionListDemoViewModel() },
            { MenuKeys.MenuKeyTagInput, () => new TagInputDemoViewModel() },
            { MenuKeys.MenuKeyThemeToggler, () => new ThemeTogglerDemoViewModel() },
            { MenuKeys.MenuKeyTreeComboBox, () => new TreeComboBoxDemoViewModel() },
        };

        return navigationItems;
    }

    public IEnumerable<(string? ParentKey, MenuItemViewModel MenuItem)> GetMenuItems()
    {
        var menuItems = new List<(string? ParentKey, MenuItemViewModel MenuItem)>();

        var buttonsAndInputs = new MenuItemViewModel
        {
            MenuHeader = "Buttons & Inputs",
            Children = new()
            {
                new() { MenuHeader = "Button Group", Key = MenuKeys.MenuKeyButtonGroup },
                new() { MenuHeader = "Icon Button", Key = MenuKeys.MenuKeyIconButton, Status = "Updated" },
                new() { MenuHeader = "AutoCompleteBox", Key = MenuKeys.MenuKeyAutoCompleteBox },
                new() { MenuHeader = "Class Input", Key = MenuKeys.MenuKeyClassInput },
                new() { MenuHeader = "Enum Selector", Key = MenuKeys.MenuKeyEnumSelector },
                new() { MenuHeader = "Form", Key = MenuKeys.MenuKeyForm },
                new() { MenuHeader = "KeyGestureInput", Key = MenuKeys.MenuKeyKeyGestureInput },
                new() { MenuHeader = "IPv4Box", Key = MenuKeys.MenuKeyIpBox },
                new() { MenuHeader = "MultiComboBox", Key = MenuKeys.MenuKeyMultiComboBox, Status = "Updated" },
                new() { MenuHeader = "Multi AutoCompleteBox", Key = MenuKeys.MenuKeyMultiAutoCompleteBox },
                new() { MenuHeader = "Numeric UpDown", Key = MenuKeys.MenuKeyNumericUpDown },
                new() { MenuHeader = "NumPad", Key = MenuKeys.MenuKeyNumPad },
                new() { MenuHeader = "PathPicker", Key = MenuKeys.MenuKeyPathPicker, Status = "Updated" },
                new() { MenuHeader = "PinCode", Key = MenuKeys.MenuKeyPinCode },
                new() { MenuHeader = "RangeSlider", Key = MenuKeys.MenuKeyRangeSlider },
                new() { MenuHeader = "Rating", Key = MenuKeys.MenuKeyRating },
                new() { MenuHeader = "Selection List", Key = MenuKeys.MenuKeySelectionList },
                new() { MenuHeader = "TagInput", Key = MenuKeys.MenuKeyTagInput },
                new() { MenuHeader = "Theme Toggler", Key = MenuKeys.MenuKeyThemeToggler },
                new() { MenuHeader = "TreeComboBox", Key = MenuKeys.MenuKeyTreeComboBox, Status = "Updated" },
            }
        };
        menuItems.Add(("Controls", buttonsAndInputs));

        return menuItems;
    }
}



