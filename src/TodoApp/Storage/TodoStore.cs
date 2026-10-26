using System.Text.Json;
using System.Text.Json.Serialization;
using ReactorTodo.Models;

namespace ReactorTodo.Storage;

/// <summary>
/// Tiny JSON file store. Reactor has no built-in disk persistence, so the sample shows
/// the usual shape: load once into initial state, save from a <c>UseEffect</c>.
/// The file lives in %LOCALAPPDATA%\ReactorTodo\todos.json.
/// </summary>
public static class TodoStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReactorTodo");

    public static string FilePath => Path.Combine(FolderPath, "todos.json");

    public static IReadOnlyList<TodoItem> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            return JsonSerializer.Deserialize(File.ReadAllText(FilePath), TodoJsonContext.Default.ListTodoItem) ?? [];
        }
        catch (Exception)
        {
            // A corrupt or unreadable file must never take the app down on startup.
            return [];
        }
    }

    public static void Save(IReadOnlyList<TodoItem> items)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(items.ToList(), TodoJsonContext.Default.ListTodoItem));
        }
        catch (Exception)
        {
            // Best-effort persistence: a locked file should not crash the UI.
        }
    }
}

/// <summary>
/// Source-generated JSON contract — keeps the app trim/AOT clean, which matters because
/// the Reactor template publishes Release builds with <c>PublishTrimmed=true</c>.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<TodoItem>))]
internal sealed partial class TodoJsonContext : JsonSerializerContext;
