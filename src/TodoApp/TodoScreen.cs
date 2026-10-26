using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Localization;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;
using ReactorTodo.Components;
using ReactorTodo.Localization;
using ReactorTodo.State;
using ReactorTodo.Storage;

namespace ReactorTodo;

public sealed record TodoScreenProps(AppSettings Settings, AppSettingsCommands SettingsCommands);

/// <summary>
/// The todo screen. It lives *inside* the LocaleProvider that <see cref="App"/> sets up, which
/// is what lets every component here (and below) read its strings from <c>UseIntl()</c>.
/// Theme and language are app-level props, so switching them re-renders from the top.
/// </summary>
public sealed class TodoScreen : Component<TodoScreenProps>
{
    public override Element Render()
    {
        var (state, dispatch) = UseReducer<TodoState, TodoAction>(TodoState.Reduce, TodoState.Initial);

        UseEffect(() => TodoStore.Save(state.Items), state.Items);

        var commands = UseMemo(() => new TodoCommands(dispatch), dispatch);
        var intl = UseIntl();
        var visible = state.VisibleItems();

        Element checklist = visible.Count == 0
            ? EmptyState(intl, state.Filter)
            : ScrollView(
                    FlexColumn([.. visible.Select(item =>
                        (Element)Component<TodoRow, TodoRowProps>(new TodoRowProps(item, commands))
                            .WithKey(item.Id))])
                    with { RowGap = 6 })
                .Flex(grow: 1, basis: 0);

        var footer = FlexRow(
                Caption(intl.T(
                        StringKeys.Summary,
                        ("remaining", state.RemainingCount),
                        ("completed", state.CompletedCount)))
                    .Foreground(Theme.SecondaryText)
                    .Flex(grow: 1, basis: 0),
                Buttons.Danger(intl.T(StringKeys.ClearCompleted), commands.RequestClearCompleted)
                    .IsEnabled(state.CompletedCount > 0)
                    .Flex(shrink: 0))
            with { ColumnGap = 8 };

        var header = VStack(
            6,
            Heading(intl.T(StringKeys.Heading)),
            Caption(intl.T(StringKeys.HeadingCaption)).Foreground(Theme.SecondaryText),
            SettingsRow(intl));

        var pendingDelete = state.PendingDeleteItem;

        var deleteDialog = ConfirmDialog.Create(
            isOpen: pendingDelete is not null,
            title: intl.T(StringKeys.DeleteDialogTitle),
            message: pendingDelete?.Title ?? string.Empty,
            warning: intl.T(StringKeys.DeleteDialogWarning),
            confirmText: intl.T(StringKeys.ConfirmDelete),
            cancelText: intl.T(StringKeys.CancelButton),
            onConfirm: commands.ConfirmDelete,
            onCancel: commands.CancelDelete);

        var clearCompletedDialog = ConfirmDialog.Create(
            isOpen: state.PendingClearCompleted,
            title: intl.T(StringKeys.ClearDialogTitle, ("count", state.CompletedCount)),
            message: intl.T(StringKeys.ClearDialogMessage),
            warning: intl.T(StringKeys.ClearDialogWarning),
            confirmText: intl.T(StringKeys.ConfirmClear),
            cancelText: intl.T(StringKeys.CancelButton),
            onConfirm: commands.ConfirmClearCompleted,
            onCancel: commands.CancelClearCompleted);

        var body = FlexColumn(
                header,
                Component<AddTodoBar, AddTodoBarProps>(new AddTodoBarProps(commands)),
                Component<FilterBar, FilterBarProps>(new FilterBarProps(
                    commands,
                    state.Filter,
                    state.Items.Count,
                    state.RemainingCount,
                    state.CompletedCount)),
                checklist,
                footer,
                deleteDialog,
                clearCompletedDialog)
            with { RowGap = 16 };

        FlexElement root = FlexColumn(
            (TitleBar(intl.T(StringKeys.ProductName)) with { Subtitle = intl.T(StringKeys.Subtitle) })
                .Flex(shrink: 0),
            body.Flex(grow: 1, basis: 0).FlexPadding(24));

        if (Props.Settings.Theme != ThemeMode.System)
        {
            // Mica follows the *system* theme, not the app's RequestedTheme, so a dark app on a
            // light system would leave a white window behind white text. When the user picks a
            // theme explicitly, paint an opaque themed surface on top of the backdrop.
            root = root.Background(Theme.SolidBackground);
        }

        return root
            .Backdrop(BackdropKind.Mica)
            // Reactor ignores .RequestedTheme() on the element a component returns (measured),
            // so write the property on the control itself. App keys this screen on the theme,
            // which remounts it and re-resolves the Theme.* brushes.
            .Set(panel => panel.RequestedTheme = Props.Settings.ToElementTheme());
    }

    /// <summary>Theme + language pickers. Both are plain ComboBoxes over the enum order.</summary>
    private Element SettingsRow(IntlAccessor intl)
    {
        var themeItems = new[]
        {
            intl.T(StringKeys.ThemeSystem),
            intl.T(StringKeys.ThemeLight),
            intl.T(StringKeys.ThemeDark),
        };

        var languageItems = new[]
        {
            intl.T(StringKeys.LanguageSystem),
            intl.T(StringKeys.LanguageChinese),
            intl.T(StringKeys.LanguageEnglish),
            intl.T(StringKeys.LanguageJapanese),
        };

        return FlexRow(
                HStack(
                    6,
                    Caption(intl.T(StringKeys.ThemeLabel)).Foreground(Theme.SecondaryText),
                    ComboBox(themeItems, (int)Props.Settings.Theme,
                        index =>
                        {
                            // A ComboBox also reports -1 when its selection is cleared; ignore
                            // that instead of pushing an out-of-range enum into the settings.
                            if (index >= 0)
                            {
                                Props.SettingsCommands.SetTheme((ThemeMode)index);
                            }
                        })),
                HStack(
                    6,
                    Caption(intl.T(StringKeys.LanguageLabel)).Foreground(Theme.SecondaryText),
                    ComboBox(languageItems, (int)Props.Settings.Language,
                        index =>
                        {
                            if (index >= 0)
                            {
                                Props.SettingsCommands.SetLanguage((AppLanguage)index);
                            }
                        })))
            with { ColumnGap = 16, AlignItems = FlexAlign.Center };
    }

    private static Element EmptyState(IntlAccessor intl, TodoFilter filter)
    {
        var (emoji, key) = filter switch
        {
            TodoFilter.Active => ("✅", StringKeys.EmptyActive),
            TodoFilter.Completed => ("🕒", StringKeys.EmptyCompleted),
            _ => ("📝", StringKeys.EmptyAll),
        };

        Element content = FlexColumn(
                TextBlock(emoji).FontSize(40),
                TextBlock(intl.T(key)).Foreground(Theme.SecondaryText))
            with { AlignItems = FlexAlign.Center, JustifyContent = FlexJustify.Center, RowGap = 8 };

        return content.Flex(grow: 1, basis: 0);
    }
}










