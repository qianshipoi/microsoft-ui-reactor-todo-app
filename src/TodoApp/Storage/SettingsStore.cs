using System.Text.Json;
using System.Text.Json.Serialization;
using ReactorTodo.State;

namespace ReactorTodo.Storage;

/// <summary>
/// Persists theme + language to %LOCALAPPDATA%\ReactorTodo\settings.json so the choices
/// survive a restart. Enums are stored by name, so the file stays readable and an unknown
/// value degrades to the default instead of throwing.
/// </summary>
public static class SettingsStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReactorTodo");

    public static string FilePath => Path.Combine(FolderPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return AppSettings.Defaults;
            }

            var stored = JsonSerializer.Deserialize(
                File.ReadAllText(FilePath),
                SettingsJsonContext.Default.StoredSettings);

            if (stored is null)
            {
                return AppSettings.Defaults;
            }

            // Enum.TryParse happily accepts out-of-range numbers ("-1"), so range-check too.
            var theme = Enum.TryParse<ThemeMode>(stored.Theme, out var parsedTheme) &&
                        Enum.IsDefined(parsedTheme)
                ? parsedTheme
                : ThemeMode.System;

            var language = Enum.TryParse<AppLanguage>(stored.Language, out var parsedLanguage) &&
                           Enum.IsDefined(parsedLanguage)
                ? parsedLanguage
                : AppLanguage.System;

            return new AppSettings(theme, language);
        }
        catch (Exception)
        {
            return AppSettings.Defaults;
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var stored = new StoredSettings(settings.Theme.ToString(), settings.Language.ToString());
            File.WriteAllText(FilePath, JsonSerializer.Serialize(stored, SettingsJsonContext.Default.StoredSettings));
        }
        catch (Exception)
        {
            // Best effort, same policy as the todo list.
        }
    }
}

internal sealed record StoredSettings(string Theme, string Language);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StoredSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
