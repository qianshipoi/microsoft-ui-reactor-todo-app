using Microsoft.UI.Reactor.Localization;

namespace ReactorTodo.Localization;

/// <summary>
/// The app's string catalog, plugged into Reactor through <see cref="IStringResourceProvider"/>.
///
/// Resources live in code here so the sample stays dependency-free and easy to extend; the
/// same interface can be backed by .resw files instead
/// (<c>Microsoft.UI.Reactor.Localization.ReswResourceProvider</c> does exactly that).
///
/// Lookup order: exact locale → language only ("ja-JP" → "ja") → default locale → the key
/// itself. Doing the fallback here matters: <c>LocaleProvider</c> passes the requested
/// locale straight through and does not fall back on its own.
/// </summary>
internal sealed class AppStringProvider : IStringResourceProvider
{
    public const string Namespace = "TodoApp";
    public const string DefaultLocale = "zh-CN";

    public static AppStringProvider Instance { get; } = new();

    private static readonly Dictionary<string, Dictionary<string, string>> Catalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = new(StringComparer.Ordinal)
            {
                [StringKeys.ProductName] = "Reactor 待办",
                [StringKeys.Subtitle] = "WinUI 3 + Microsoft.UI.Reactor",
                [StringKeys.Heading] = "待办清单",
                [StringKeys.HeadingCaption] = "Microsoft.UI.Reactor 示例 · 纯 C# 声明式 UI，无 XAML、无 ViewModel",

                [StringKeys.AddPlaceholder] = "添加一项待办，按 Enter 确认",
                [StringKeys.AddButton] = "添加",
                [StringKeys.EditButton] = "编辑",
                [StringKeys.DeleteButton] = "删除",
                [StringKeys.SaveButton] = "保存",
                [StringKeys.CancelButton] = "取消",

                [StringKeys.FilterAll] = "全部 {count}",
                [StringKeys.FilterActive] = "未完成 {count}",
                [StringKeys.FilterCompleted] = "已完成 {count}",
                [StringKeys.ClearCompleted] = "清除已完成",
                [StringKeys.Summary] = "{remaining} 项未完成 · {completed} 项已完成",

                [StringKeys.EmptyAll] = "列表还是空的，在上方输入第一条待办吧",
                [StringKeys.EmptyActive] = "没有未完成的事项，全部搞定了",
                [StringKeys.EmptyCompleted] = "还没有已完成的事项",

                [StringKeys.ThemeLabel] = "主题",
                [StringKeys.ThemeSystem] = "跟随系统",
                [StringKeys.ThemeLight] = "浅色",
                [StringKeys.ThemeDark] = "深色",
                [StringKeys.LanguageLabel] = "语言",
                [StringKeys.LanguageSystem] = "跟随系统",
                [StringKeys.LanguageChinese] = "简体中文",
                [StringKeys.LanguageEnglish] = "English",
                [StringKeys.LanguageJapanese] = "日本語",

                [StringKeys.DeleteDialogTitle] = "删除这条待办？",
                [StringKeys.DeleteDialogWarning] = "删除后无法恢复。",
                [StringKeys.ClearDialogTitle] = "清除 {count} 项已完成的待办？",
                [StringKeys.ClearDialogMessage] = "只删除已勾选完成的条目，未完成的待办不受影响。",
                [StringKeys.ClearDialogWarning] = "此操作无法撤销。",
                [StringKeys.ConfirmDelete] = "删除",
                [StringKeys.ConfirmClear] = "清除",

                [StringKeys.AutomationNewTodo] = "新待办内容",
                [StringKeys.AutomationToggle] = "切换「{title}」的完成状态",
                [StringKeys.AutomationEdit] = "编辑「{title}」",
            },

            ["en-US"] = new(StringComparer.Ordinal)
            {
                [StringKeys.ProductName] = "Reactor Todo",
                [StringKeys.Subtitle] = "WinUI 3 + Microsoft.UI.Reactor",
                [StringKeys.Heading] = "Todo list",
                [StringKeys.HeadingCaption] = "A Microsoft.UI.Reactor sample · declarative C#, no XAML, no view models",

                [StringKeys.AddPlaceholder] = "Add a todo, then press Enter",
                [StringKeys.AddButton] = "Add",
                [StringKeys.EditButton] = "Edit",
                [StringKeys.DeleteButton] = "Delete",
                [StringKeys.SaveButton] = "Save",
                [StringKeys.CancelButton] = "Cancel",

                [StringKeys.FilterAll] = "All {count}",
                [StringKeys.FilterActive] = "Active {count}",
                [StringKeys.FilterCompleted] = "Done {count}",
                [StringKeys.ClearCompleted] = "Clear completed",
                [StringKeys.Summary] = "{remaining} remaining · {completed} done",

                [StringKeys.EmptyAll] = "Nothing here yet — add your first todo above",
                [StringKeys.EmptyActive] = "All done — nothing left to do",
                [StringKeys.EmptyCompleted] = "Nothing completed yet",

                [StringKeys.ThemeLabel] = "Theme",
                [StringKeys.ThemeSystem] = "Use system setting",
                [StringKeys.ThemeLight] = "Light",
                [StringKeys.ThemeDark] = "Dark",
                [StringKeys.LanguageLabel] = "Language",
                [StringKeys.LanguageSystem] = "Use system setting",
                [StringKeys.LanguageChinese] = "简体中文",
                [StringKeys.LanguageEnglish] = "English",
                [StringKeys.LanguageJapanese] = "日本語",

                [StringKeys.DeleteDialogTitle] = "Delete this todo?",
                [StringKeys.DeleteDialogWarning] = "This cannot be undone.",
                [StringKeys.ClearDialogTitle] = "Clear {count} completed todos?",
                [StringKeys.ClearDialogMessage] = "Only completed rows are removed; active todos stay.",
                [StringKeys.ClearDialogWarning] = "This cannot be undone.",
                [StringKeys.ConfirmDelete] = "Delete",
                [StringKeys.ConfirmClear] = "Clear",

                [StringKeys.AutomationNewTodo] = "New todo text",
                [StringKeys.AutomationToggle] = "Toggle completion of \"{title}\"",
                [StringKeys.AutomationEdit] = "Edit \"{title}\"",
            },

            ["ja-JP"] = new(StringComparer.Ordinal)
            {
                [StringKeys.ProductName] = "Reactor やること",
                [StringKeys.Subtitle] = "WinUI 3 + Microsoft.UI.Reactor",
                [StringKeys.Heading] = "やることリスト",
                [StringKeys.HeadingCaption] = "Microsoft.UI.Reactor サンプル · XAML も ViewModel も使わない宣言的 C#",

                [StringKeys.AddPlaceholder] = "やることを入力して Enter で追加",
                [StringKeys.AddButton] = "追加",
                [StringKeys.EditButton] = "編集",
                [StringKeys.DeleteButton] = "削除",
                [StringKeys.SaveButton] = "保存",
                [StringKeys.CancelButton] = "キャンセル",

                [StringKeys.FilterAll] = "すべて {count}",
                [StringKeys.FilterActive] = "未完了 {count}",
                [StringKeys.FilterCompleted] = "完了 {count}",
                [StringKeys.ClearCompleted] = "完了を消去",
                [StringKeys.Summary] = "残り {remaining} 件 · 完了 {completed} 件",

                [StringKeys.EmptyAll] = "まだ空です。上で最初の項目を追加してください",
                [StringKeys.EmptyActive] = "未完了はありません。全部片付きました",
                [StringKeys.EmptyCompleted] = "完了した項目はまだありません",

                [StringKeys.ThemeLabel] = "テーマ",
                [StringKeys.ThemeSystem] = "システムに合わせる",
                [StringKeys.ThemeLight] = "ライト",
                [StringKeys.ThemeDark] = "ダーク",
                [StringKeys.LanguageLabel] = "言語",
                [StringKeys.LanguageSystem] = "システムに合わせる",
                [StringKeys.LanguageChinese] = "简体中文",
                [StringKeys.LanguageEnglish] = "English",
                [StringKeys.LanguageJapanese] = "日本語",

                [StringKeys.DeleteDialogTitle] = "この項目を削除しますか？",
                [StringKeys.DeleteDialogWarning] = "削除すると元に戻せません。",
                [StringKeys.ClearDialogTitle] = "完了した {count} 件を消去しますか？",
                [StringKeys.ClearDialogMessage] = "完了した項目だけを消します。未完了はそのままです。",
                [StringKeys.ClearDialogWarning] = "この操作は取り消せません。",
                [StringKeys.ConfirmDelete] = "削除",
                [StringKeys.ConfirmClear] = "消去",

                [StringKeys.AutomationNewTodo] = "新しい項目の内容",
                [StringKeys.AutomationToggle] = "「{title}」の完了状態を切り替え",
                [StringKeys.AutomationEdit] = "「{title}」を編集",
            },
        };

    /// <summary>The locales this app ships; used by the language picker.</summary>
    public static IReadOnlyList<string> SupportedLocales { get; } = ["zh-CN", "en-US", "ja-JP"];

    public string GetString(string locale, string ns, string key)
    {
        if (TryLookup(locale, key, out var value) ||
            TryLookup(LanguageOf(locale), key, out value) ||
            TryLookup(DefaultLocale, key, out value))
        {
            return value;
        }

        // Visible marker instead of throwing: a missing key should never take the UI down.
        return key;
    }

    private static string LanguageOf(string locale)
    {
        var dash = locale.IndexOf('-');
        return dash > 0 ? locale[..dash] : locale;
    }

    private static bool TryLookup(string locale, string key, out string value)
    {
        value = string.Empty;
        if (!Catalog.TryGetValue(locale, out var map) || !map.TryGetValue(key, out var found))
        {
            return false;
        }

        value = found;
        return true;
    }
}
