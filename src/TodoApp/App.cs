using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Windows.UI;
using Windows.UI.ViewManagement;
using static Microsoft.UI.Reactor.Factories;
using ReactorTodo.Localization;
using ReactorTodo.State;
using ReactorTodo.Storage;

namespace ReactorTodo;

/// <summary>
/// Root component. It owns app-level preferences (theme + language) and wraps the screen in a
/// <c>LocaleProvider</c>, so anything below — including rows and dialogs — can read localized
/// strings through <c>UseIntl()</c> and re-render when the language changes.
/// </summary>
public sealed class App : Component
{
    /// <summary>Shared empty dependency list: a fresh [] would compare unequal on every render.</summary>
    private static readonly object[] NoDependencies = [];

    public override Element Render()
    {
        var (settings, dispatch) = UseReducer<AppSettings, AppSettingsAction>(
            AppSettings.Reduce,
            AppSettings.Initial);

        // Persist preferences; `settings` is a record, so this only runs when one actually changed.
        UseEffect(() => SettingsStore.Save(settings), settings);

        // ---- theme ---------------------------------------------------------------------
        // "Follow system" tracks the system's light/dark choice live (and only that — the UI is
        // built from neutral Theme.* tokens, so the user's custom accent never leaks in).
        var (systemDark, setSystemDark) = UseState(SystemTheme.IsDark());
        UseEffect(
            () =>
            {
                var queue = DispatcherQueue.GetForCurrentThread();
                var uiSettings = new UISettings();
                void OnColorValuesChanged(UISettings sender, object args)
                    => queue?.TryEnqueue(() => setSystemDark(SystemTheme.IsDark()));

                uiSettings.ColorValuesChanged += OnColorValuesChanged;
                return () => uiSettings.ColorValuesChanged -= OnColorValuesChanged;
            },
            NoDependencies);

        var resolvedTheme = settings.Theme == ThemeMode.System
            ? (systemDark ? ThemeMode.Dark : ThemeMode.Light)
            : settings.Theme;

        var settingsCommands = UseMemo(() => new AppSettingsCommands(dispatch), dispatch);

        // Caption buttons are drawn by the system and follow the *app* theme, which WinUI does
        // not let us change at runtime, so colour them to match the resolved theme.
        ApplyWindowChrome(UseWindow(), resolvedTheme);

        // Keying the screen element on the resolved theme remounts it when the theme changes,
        // which is what makes Reactor resolve its Theme.* brushes against the new theme.
        // It deliberately adds no wrapper element: an extra FlexColumn would cut the height
        // chain and the list would stop filling the window.
        return LocaleProvider(
            settings.Locale,
            Component<TodoScreen, TodoScreenProps>(
                    new TodoScreenProps(settings, resolvedTheme, settingsCommands))
                .WithKey($"theme-{settings.Theme}-{resolvedTheme}"),
            AppStringProvider.Instance,
            AppStringProvider.DefaultLocale);
    }

    /// <summary>Keeps the system-drawn caption buttons readable against the app theme.</summary>
    private static void ApplyWindowChrome(ReactorWindow? window, ThemeMode theme)
    {
        try
        {
            var titleBar = window?.AppWindow.TitleBar;
            if (titleBar is null)
            {
                return;
            }

            var dark = theme == ThemeMode.Dark;
            var foreground = dark ? Colors.White : Colors.Black;
            var inactive = dark ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x99, 0, 0, 0);
            var hover = dark ? Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1A, 0, 0, 0);
            var pressed = dark ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x33, 0, 0, 0);

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = inactive;
            titleBar.ButtonHoverBackgroundColor = hover;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = pressed;
            titleBar.ButtonPressedForegroundColor = foreground;
        }
        catch (Exception)
        {
            // Chrome colours are cosmetic; never let them break rendering.
        }
    }
}

