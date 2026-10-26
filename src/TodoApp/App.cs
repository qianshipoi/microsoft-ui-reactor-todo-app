using Microsoft.UI;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Windows.UI;
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
    public override Element Render()
    {
        var (settings, dispatch) = UseReducer<AppSettings, AppSettingsAction>(
            AppSettings.Reduce,
            AppSettings.Initial);

        // Persist preferences; `settings` is a record, so this only runs when one actually changed.
        UseEffect(() => SettingsStore.Save(settings), settings);

        var settingsCommands = UseMemo(() => new AppSettingsCommands(dispatch), dispatch);

        // The caption buttons are drawn by the system and follow the app theme, which WinUI does
        // not allow changing at runtime, so recolour them to match our content.
        ApplyWindowChrome(UseWindow(), settings.Theme);

        // Keying the screen element on the theme remounts the whole screen when the theme
        // changes. That is what makes Reactor resolve its Theme.* brushes against the new
        // theme — they are resolved at element-creation time, not on demand.
        // It deliberately does NOT introduce a wrapper element: an extra FlexColumn here would
        // cut the height chain and the list would stop filling the window.
        return LocaleProvider(
            settings.Locale,
            Component<TodoScreen, TodoScreenProps>(new TodoScreenProps(settings, settingsCommands))
                .WithKey($"theme-{settings.Theme}"),
            AppStringProvider.Instance,
            AppStringProvider.DefaultLocale);
    }

    /// <summary>Keeps the system-drawn caption buttons readable when the app theme is overridden.</summary>
    private static void ApplyWindowChrome(ReactorWindow? window, ThemeMode mode)
    {
        try
        {
            var titleBar = window?.AppWindow.TitleBar;
            if (titleBar is null)
            {
                return;
            }

            if (mode == ThemeMode.System)
            {
                // Hand the colours back to WinUI.
                titleBar.ButtonBackgroundColor = null;
                titleBar.ButtonInactiveBackgroundColor = null;
                titleBar.ButtonForegroundColor = null;
                titleBar.ButtonInactiveForegroundColor = null;
                titleBar.ButtonHoverBackgroundColor = null;
                titleBar.ButtonHoverForegroundColor = null;
                titleBar.ButtonPressedBackgroundColor = null;
                titleBar.ButtonPressedForegroundColor = null;
                return;
            }

            var dark = mode == ThemeMode.Dark;
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
