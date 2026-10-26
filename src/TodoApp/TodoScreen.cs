using Microsoft.UI;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Reactor.Localization;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;
using static Microsoft.UI.Reactor.Factories;
using ReactorTodo.Animations;
using ReactorTodo.Components;
using ReactorTodo.Localization;
using ReactorTodo.State;
using ReactorTodo.Storage;

namespace ReactorTodo;

public sealed record TodoScreenProps(AppSettings Settings, ThemeMode ResolvedTheme, AppSettingsCommands SettingsCommands);

/// <summary>
/// The todo screen. It lives *inside* the LocaleProvider that <see cref="App"/> sets up, which is
/// what lets every component here (and below) read its strings from <c>UseIntl()</c>.
/// Theme and language are app-level props, so switching them re-renders from the top.
/// </summary>
public sealed class TodoScreen : Component<TodoScreenProps>
{
    // Segoe Fluent Icons only ships with Windows 11; Segoe MDL2 Assets is present on every
    // supported Windows 10 build too, and carries the same glyphs.
    private const string IconFont = "Segoe MDL2 Assets";

    /// <summary>Globe glyph (E774) for the language picker.</summary>
    private const string LanguageGlyph = "\uE774";

    /// <summary>
    /// Fixed 20x20 icon box shared by every settings button, so both buttons end up the same size
    /// and their artwork is optically aligned (a glyph at 16 px, the Lottie at 20 px with its
    /// artwork pre-scaled to the same ink size).
    /// </summary>
    private static Element IconHost(Element content) => Border(content).Width(20).Height(20);

    /// <summary>Which mode the icon on screen belongs to. Survives the screen remount.</summary>
    private static ThemeMode s_renderedMode = ThemeMode.System;

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

        // Settings live at the top-right of the page content (not in the title bar, so the window
        // frame keeps the plain Win10 look).
        var header = FlexRow(
                VStack(
                    6,
                    Heading(intl.T(StringKeys.Heading)),
                    Caption(intl.T(StringKeys.HeadingCaption)).Foreground(Theme.SecondaryText))
                    .Flex(grow: 1, basis: 0),
                HStack(4, ThemeButton(intl), LanguageButton(intl))
                    .Flex(shrink: 0, alignSelf: FlexAlign.Start))
            with { ColumnGap = 12 };

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

        // The themed surface is painted explicitly instead of relying on the Mica backdrop: the
        // system backdrop follows the *system* theme and would leave a white window behind white
        // text whenever the app theme differs from it. "Follow system" is resolved to a concrete
        // light/dark value upstream, so this is always correct.
        return FlexColumn(
                (TitleBar(intl.T(StringKeys.ProductName)) with { Subtitle = intl.T(StringKeys.Subtitle) })
                    .Flex(shrink: 0),
                body.Flex(grow: 1, basis: 0).FlexPadding(24))
            .Background(Theme.SolidBackground)
            // Reactor ignores .RequestedTheme() on the element a component returns (measured),
            // so write the property on the control itself. App keys this screen on the selected
            // mode, which remounts it and re-resolves the Theme.* brushes.
            .Set(panel => panel.RequestedTheme = Props.ResolvedTheme.ToElementTheme());
    }

    /// <summary>
    /// Theme is a three-state toggle that never grows, so a click just cycles
    /// follow-system -> light -> dark -> follow-system instead of opening a flyout.
    /// </summary>
    private Element ThemeButton(IntlAccessor intl)
    {
        var mode = Props.Settings.Theme;
        var next = mode switch
        {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.System,
        };

        var currentLabel = mode switch
        {
            ThemeMode.Light => intl.T(StringKeys.ThemeLight),
            ThemeMode.Dark => intl.T(StringKeys.ThemeDark),
            _ => intl.T(StringKeys.ThemeSystem),
        };

        return Button(IconHost(ThemeIcon()), () => Props.SettingsCommands.SetTheme(next))
            .SubtleButton()
            .AutomationId("ThemeButton")
            .AutomationName($"{intl.T(StringKeys.ThemeLabel)}: {currentLabel}")
            .Flex(shrink: 0);
    }

    /// <summary>
    /// Theme icon: one Lottie animation covering all three states, generated by
    /// <c>tools/gen-theme-lottie.mjs</c> and compiled by LottieGen
    /// (<c>-Language cs -WinUIVersion 3.0 -GenerateColorBindings</c>) into <see cref="Theme_3state"/>.
    ///
    /// Frame map (fr 60, frames 0..30 = 517 ms, 250 ms per leg):
    /// progress 0 = follow system (monitor), 0.5 = light (sun), 1 = dark (moon).
    /// Switching plays the morph between the two progress values in either direction, so all
    /// three transitions animate.
    /// </summary>
    private Element ThemeIcon()
    {
        var mode = Props.Settings.Theme;
        var previous = s_renderedMode;
        s_renderedMode = mode;

        var (from, to) = Transition(previous, mode);

        // One colour in the asset, exposed by -GenerateColorBindings; recolour per theme so the
        // icon stays readable on the dark surface.
        var source = new Theme_3state();
        source.SetColorProperty(
            "Color_000000",
            Props.ResolvedTheme == ThemeMode.Dark ? Colors.White : Color.FromArgb(0xE5, 0, 0, 0));

        AnimatedVisualPlayer? player = null;
        return AnimatedVisualPlayer()
            .Set(control =>
            {
                player = control;
                // Colours must be applied to the source before it is handed to the player.
                control.Source ??= source;
                control.AutoPlay = false;
                // Freeze on this mode's frame, so the icon is right even before/without animating.
                control.SetProgress(to);

            })
            .OnMount(_element =>
            {
                if (player is null || Math.Abs(from - to) < 0.001)
                {
                    return;
                }

                // Morph between the two states; PlayAsync leaves the visual on the final frame.
                _ = player.PlayAsync(from, to, false);   // fire and forget
            });
    }

    /// <summary>One leg of the Lottie timeline: 15 of 45 frames.</summary>
    private const double LegOne = 15.0 / 45.0;

    /// <summary>Two legs of the Lottie timeline.</summary>
    private const double LegTwo = 30.0 / 45.0;

    /// <summary>
    /// Which slice of the animation to play, as a forward-only range.
    ///
    /// WinUI's <c>AnimatedVisualPlayer.PlayAsync(from, to)</c> does not play in reverse: asking for
    /// 1 -> 0 runs forward and finishes on progress 1, which left the icon stuck on the moon after
    /// dark -> follow-system. The asset therefore holds three sequential legs
    /// (system -> light -> dark -> system) and every transition plays forwards:
    ///
    ///   0 -> 1/3   system -> light
    ///   1/3 -> 2/3 light  -> dark
    ///   2/3 -> 1   dark   -> system
    /// </summary>
    private static (double From, double To) Transition(ThemeMode previous, ThemeMode current)
        => current switch
        {
            ThemeMode.Light => (0, LegOne),
            ThemeMode.Dark => previous == ThemeMode.System ? (0, LegTwo) : (LegOne, LegTwo),
            // System: the third leg when coming from dark, otherwise the static frame 0.
            _ => previous == ThemeMode.Dark ? (LegTwo, 1) : (0, 0),
        };

    private Element LanguageButton(IntlAccessor intl)
        => SettingsButton(
            LanguageGlyph,
            "LanguageButton",
            intl.T(StringKeys.LanguageLabel),
            MenuItems(
                RadioMenuItem(
                    intl.T(StringKeys.LanguageSystem), "language",
                    Props.Settings.Language == AppLanguage.System,
                    () => Props.SettingsCommands.SetLanguage(AppLanguage.System)),
                RadioMenuItem(
                    intl.T(StringKeys.LanguageChinese), "language",
                    Props.Settings.Language == AppLanguage.ChineseSimplified,
                    () => Props.SettingsCommands.SetLanguage(AppLanguage.ChineseSimplified)),
                RadioMenuItem(
                    intl.T(StringKeys.LanguageEnglish), "language",
                    Props.Settings.Language == AppLanguage.English,
                    () => Props.SettingsCommands.SetLanguage(AppLanguage.English)),
                RadioMenuItem(
                    intl.T(StringKeys.LanguageJapanese), "language",
                    Props.Settings.Language == AppLanguage.Japanese,
                    () => Props.SettingsCommands.SetLanguage(AppLanguage.Japanese))));

    /// <summary>
    /// Icon-only button that opens a check-marked menu flyout. The automation id stays stable
    /// across languages, which keeps UI tests (and screen-reader tooling) locale-independent.
    /// </summary>
    private static Element SettingsButton(string glyph, string automationId, string automationName, Element flyout)
        => Button(IconHost(Icon(FontIcon(glyph, IconFont, 16))), null)
            .SubtleButton()
            .AutomationId(automationId)
            .AutomationName(automationName)
            .WithIconPressScale()
            .WithFlyout(flyout)
            .Flex(shrink: 0);

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








