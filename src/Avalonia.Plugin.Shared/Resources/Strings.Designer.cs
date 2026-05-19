#nullable enable
namespace Avalonia.Plugin.Shared.Resources;

public static class Strings
{
    private static global::System.Resources.ResourceManager? _resourceManager;

    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new global::System.Resources.ResourceManager(
                    "Avalonia.Plugin.Shared.Resources.Strings",
                    typeof(Strings).Assembly);
            }
            return _resourceManager;
        }
    }

    public static string MENU_BRING_TO_FRONT => ResourceManager.GetString(nameof(MENU_BRING_TO_FRONT), Culture)!;
    public static string MENU_BRING_FORWARD => ResourceManager.GetString(nameof(MENU_BRING_FORWARD), Culture)!;
    public static string MENU_SEND_BACKWARD => ResourceManager.GetString(nameof(MENU_SEND_BACKWARD), Culture)!;
    public static string MENU_SEND_TO_BACK => ResourceManager.GetString(nameof(MENU_SEND_TO_BACK), Culture)!;
    public static string MENU_DIALOG_OK => ResourceManager.GetString(nameof(MENU_DIALOG_OK), Culture)!;
    public static string MENU_DIALOG_CANCEL => ResourceManager.GetString(nameof(MENU_DIALOG_CANCEL), Culture)!;
    public static string MENU_DIALOG_YES => ResourceManager.GetString(nameof(MENU_DIALOG_YES), Culture)!;
    public static string MENU_DIALOG_NO => ResourceManager.GetString(nameof(MENU_DIALOG_NO), Culture)!;
    public static string MENU_DIALOG_CLOSE => ResourceManager.GetString(nameof(MENU_DIALOG_CLOSE), Culture)!;
    public static string MENU_CUT => ResourceManager.GetString(nameof(MENU_CUT), Culture)!;
    public static string MENU_COPY => ResourceManager.GetString(nameof(MENU_COPY), Culture)!;
    public static string MENU_PASTE => ResourceManager.GetString(nameof(MENU_PASTE), Culture)!;
    public static string MENU_CLEAR => ResourceManager.GetString(nameof(MENU_CLEAR), Culture)!;
    public static string PAGINATION_JUMP_TO => ResourceManager.GetString(nameof(PAGINATION_JUMP_TO), Culture)!;
    public static string PAGINATION_PAGE => ResourceManager.GetString(nameof(PAGINATION_PAGE), Culture)!;
    public static string THEME_TOGGLE_DARK => ResourceManager.GetString(nameof(THEME_TOGGLE_DARK), Culture)!;
    public static string THEME_TOGGLE_LIGHT => ResourceManager.GetString(nameof(THEME_TOGGLE_LIGHT), Culture)!;
    public static string THEME_TOGGLE_SYSTEM => ResourceManager.GetString(nameof(THEME_TOGGLE_SYSTEM), Culture)!;
    public static string DATE_TIME_CONFIRM => ResourceManager.GetString(nameof(DATE_TIME_CONFIRM), Culture)!;
    public static string DATE_TIME_START_TIME => ResourceManager.GetString(nameof(DATE_TIME_START_TIME), Culture)!;
    public static string DATE_TIME_END_TIME => ResourceManager.GetString(nameof(DATE_TIME_END_TIME), Culture)!;
    public static string CHOOSER_DIALOG_OK => ResourceManager.GetString(nameof(CHOOSER_DIALOG_OK), Culture)!;
    public static string CHOOSER_DIALOG_CANCEL => ResourceManager.GetString(nameof(CHOOSER_DIALOG_CANCEL), Culture)!;
    public static string CHOOSER_FILE_NAME => ResourceManager.GetString(nameof(CHOOSER_FILE_NAME), Culture)!;
    public static string CHOOSER_SHOW_HIDDEN_FILES => ResourceManager.GetString(nameof(CHOOSER_SHOW_HIDDEN_FILES), Culture)!;
    public static string CHOOSER_NAME_COLUMN => ResourceManager.GetString(nameof(CHOOSER_NAME_COLUMN), Culture)!;
    public static string CHOOSER_DATEMODIFIED_COLUMN => ResourceManager.GetString(nameof(CHOOSER_DATEMODIFIED_COLUMN), Culture)!;
    public static string CHOOSER_TYPE_COLUMN => ResourceManager.GetString(nameof(CHOOSER_TYPE_COLUMN), Culture)!;
    public static string CHOOSER_SIZE_COLUMN => ResourceManager.GetString(nameof(CHOOSER_SIZE_COLUMN), Culture)!;
    public static string CHOOSER_PROMPT_FILE_ALREADY_EXISTS => ResourceManager.GetString(nameof(CHOOSER_PROMPT_FILE_ALREADY_EXISTS), Culture)!;
    public static string DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER => ResourceManager.GetString(nameof(DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER), Culture)!;
    public static string BREADCRUMB_HOME => ResourceManager.GetString(nameof(BREADCRUMB_HOME), Culture)!;
    public static string NAV_Introduction => ResourceManager.GetString(nameof(NAV_Introduction), Culture)!;
    public static string NAV_Settings => ResourceManager.GetString(nameof(NAV_Settings), Culture)!;
    public static string NAV_Plugins => ResourceManager.GetString(nameof(NAV_Plugins), Culture)!;
    public static string NAV_PluginManagement => ResourceManager.GetString(nameof(NAV_PluginManagement), Culture)!;
    public static string NAV_AboutUs => ResourceManager.GetString(nameof(NAV_AboutUs), Culture)!;

    public static global::System.Globalization.CultureInfo Culture
    {
        get => global::System.Globalization.CultureInfo.CurrentUICulture;
        set => global::System.Globalization.CultureInfo.CurrentUICulture = value;
    }
}
