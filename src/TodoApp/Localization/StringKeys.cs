namespace ReactorTodo.Localization;

/// <summary>
/// String keys used with Reactor's localization stack. Keeping them as constants means a
/// typo shows up at compile time instead of as a missing-string marker in the UI.
/// </summary>
internal static class StringKeys
{
    // chrome
    public const string ProductName = "ProductName";
    public const string Subtitle = "Subtitle";
    public const string Heading = "Heading";
    public const string HeadingCaption = "HeadingCaption";

    // composer + row
    public const string AddPlaceholder = "AddPlaceholder";
    public const string AddButton = "AddButton";
    public const string EditButton = "EditButton";
    public const string DeleteButton = "DeleteButton";
    public const string SaveButton = "SaveButton";
    public const string CancelButton = "CancelButton";

    // filters + summary
    public const string FilterAll = "FilterAll";
    public const string FilterActive = "FilterActive";
    public const string FilterCompleted = "FilterCompleted";
    public const string ClearCompleted = "ClearCompleted";
    public const string Summary = "Summary";

    // empty states
    public const string EmptyAll = "EmptyAll";
    public const string EmptyActive = "EmptyActive";
    public const string EmptyCompleted = "EmptyCompleted";

    // settings
    public const string ThemeLabel = "ThemeLabel";
    public const string ThemeSystem = "ThemeSystem";
    public const string ThemeLight = "ThemeLight";
    public const string ThemeDark = "ThemeDark";
    public const string LanguageLabel = "LanguageLabel";
    public const string LanguageSystem = "LanguageSystem";
    public const string LanguageChinese = "LanguageChinese";
    public const string LanguageEnglish = "LanguageEnglish";
    public const string LanguageJapanese = "LanguageJapanese";

    // confirmation dialogs
    public const string DeleteDialogTitle = "DeleteDialogTitle";
    public const string DeleteDialogWarning = "DeleteDialogWarning";
    public const string ClearDialogTitle = "ClearDialogTitle";
    public const string ClearDialogMessage = "ClearDialogMessage";
    public const string ClearDialogWarning = "ClearDialogWarning";
    public const string ConfirmDelete = "ConfirmDelete";
    public const string ConfirmClear = "ConfirmClear";

    // automation names
    public const string AutomationNewTodo = "AutomationNewTodo";
    public const string AutomationToggle = "AutomationToggle";
    public const string AutomationEdit = "AutomationEdit";
}
