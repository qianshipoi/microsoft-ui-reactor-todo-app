using System.Globalization;
using Microsoft.UI.Xaml;
using ReactorTodo.Localization;
using ReactorTodo.Storage;

namespace ReactorTodo.State;

/// <summary>Light / dark / follow-the-system.</summary>
public enum ThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>UI language; <see cref="System"/> follows the OS UI language.</summary>
public enum AppLanguage
{
    System = 0,
    ChineseSimplified = 1,
    English = 2,
    Japanese = 3,
}

public abstract record AppSettingsAction;

public sealed record SetThemeMode(ThemeMode Mode) : AppSettingsAction;

public sealed record SetLanguage(AppLanguage Language) : AppSettingsAction;

/// <summary>
/// App-level preferences (theme + language), kept separate from the todo list so switching
/// a theme never rewrites the data file. Persisted to settings.json.
/// </summary>
public sealed record AppSettings(ThemeMode Theme, AppLanguage Language)
{
    public static AppSettings Defaults { get; } = new(ThemeMode.System, AppLanguage.System);

    public static AppSettings Initial => SettingsStore.Load();

    /// <summary>Locale handed to Reactor's LocaleProvider.</summary>
    public string Locale => Language switch
    {
        AppLanguage.ChineseSimplified => "zh-CN",
        AppLanguage.English => "en-US",
        AppLanguage.Japanese => "ja-JP",
        _ => SystemLocale,
    };

    /// <summary>The OS UI language, mapped onto a locale this app ships.</summary>
    public static string SystemLocale
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture;
            if (AppStringProvider.SupportedLocales.Contains(culture.Name))
            {
                return culture.Name;
            }

            return culture.TwoLetterISOLanguageName switch
            {
                "zh" => "zh-CN",
                "ja" => "ja-JP",
                "en" => "en-US",
                _ => AppStringProvider.DefaultLocale,
            };
        }
    }

    /// <summary>ElementTheme.Default means "follow the system" in WinUI.</summary>
    public ElementTheme ToElementTheme() => Theme switch
    {
        ThemeMode.Light => ElementTheme.Light,
        ThemeMode.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    public static AppSettings Reduce(AppSettings settings, AppSettingsAction action) => action switch
    {
        SetThemeMode t => settings with { Theme = t.Mode },
        SetLanguage l => settings with { Language = l.Language },
        _ => settings,
    };
}

/// <summary>Stable facade over the settings dispatch, same shape as TodoCommands.</summary>
public sealed class AppSettingsCommands(Action<AppSettingsAction> dispatch)
{
    public void SetTheme(ThemeMode mode) => dispatch(new SetThemeMode(mode));

    public void SetLanguage(AppLanguage language) => dispatch(new SetLanguage(language));
}
