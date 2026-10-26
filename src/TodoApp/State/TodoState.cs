using ReactorTodo.Models;
using ReactorTodo.Storage;

namespace ReactorTodo.State;

/// <summary>Which slice of the list the user is looking at.</summary>
public enum TodoFilter
{
    All,
    Active,
    Completed,
}

/// <summary>Everything the user can do to the list. Modelled as a closed set of records.</summary>
public abstract record TodoAction;

public sealed record AddTodo(string Title) : TodoAction;

public sealed record SetTodoDone(string Id, bool Done) : TodoAction;

public sealed record RenameTodo(string Id, string Title) : TodoAction;

/// <summary>Opens the "really delete this one?" confirmation.</summary>
public sealed record RequestDelete(string Id) : TodoAction;

public sealed record CancelDelete : TodoAction;

/// <summary>Applies the deletion that <see cref="RequestDelete"/> asked about.</summary>
public sealed record ConfirmDelete : TodoAction;

/// <summary>Opens the "really clear the completed items?" confirmation.</summary>
public sealed record RequestClearCompleted : TodoAction;

public sealed record CancelClearCompleted : TodoAction;

/// <summary>Applies the removal that <see cref="RequestClearCompleted"/> asked about.</summary>
public sealed record ConfirmClearCompleted : TodoAction;

public sealed record SetFilter(TodoFilter Filter) : TodoAction;

/// <summary>
/// Single immutable state object for the whole screen — the "one reducer" shape that
/// Reactor's <c>UseReducer&lt;TState, TAction&gt;</c> expects. It also carries the two
/// pending-confirmation fields, so an open dialog is just state: rendering it, opening it
/// and closing it all go through the same reducer.
/// </summary>
public sealed record TodoState(
    IReadOnlyList<TodoItem> Items,
    TodoFilter Filter,
    string? PendingDeleteId = null,
    bool PendingClearCompleted = false)
{
    /// <summary>
    /// Loaded once per process, so the very first render already shows persisted todos
    /// (no flash of an empty list before an effect runs).
    /// </summary>
    public static TodoState Initial { get; } = new(TodoStore.Load(), TodoFilter.All);

    public int RemainingCount => Items.Count(item => !item.Done);

    public int CompletedCount => Items.Count(item => item.Done);

    /// <summary>The item the delete dialog is asking about, if any.</summary>
    public TodoItem? PendingDeleteItem =>
        PendingDeleteId is null ? null : Items.FirstOrDefault(item => item.Id == PendingDeleteId);

    public IReadOnlyList<TodoItem> VisibleItems() => Filter switch
    {
        TodoFilter.Active => [.. Items.Where(item => !item.Done)],
        TodoFilter.Completed => [.. Items.Where(item => item.Done)],
        _ => Items,
    };

    public static TodoState Reduce(TodoState state, TodoAction action)
    {
        switch (action)
        {
            case AddTodo a when !string.IsNullOrWhiteSpace(a.Title):
                return state with { Items = [.. state.Items, TodoItem.Create(a.Title)] };

            case SetTodoDone s:
                return state with { Items = [.. state.Items.Select(i => i.Id == s.Id ? i with { Done = s.Done } : i)] };

            case RenameTodo r when !string.IsNullOrWhiteSpace(r.Title):
                return state with { Items = [.. state.Items.Select(i => i.Id == r.Id ? i with { Title = r.Title.Trim() } : i)] };

            // --- confirmations: request -> (cancel | confirm) -------------------
            case RequestDelete rd:
                return state with { PendingDeleteId = rd.Id };

            case CancelDelete:
                return state.PendingDeleteId is null ? state : state with { PendingDeleteId = null };

            case ConfirmDelete:
                return state.PendingDeleteId is null
                    ? state
                    : state with
                    {
                        Items = [.. state.Items.Where(i => i.Id != state.PendingDeleteId)],
                        PendingDeleteId = null,
                    };

            case RequestClearCompleted:
                return state.PendingClearCompleted ? state : state with { PendingClearCompleted = true };

            case CancelClearCompleted:
                return state.PendingClearCompleted ? state with { PendingClearCompleted = false } : state;

            case ConfirmClearCompleted:
                return state.PendingClearCompleted
                    ? state with
                    {
                        Items = [.. state.Items.Where(i => !i.Done)],
                        PendingClearCompleted = false,
                    }
                    : state;

            // --- mutations, only reachable through a confirmation ----------------
            case SetFilter f:
                return state with { Filter = f.Filter };

            default:
                return state;
        }
    }
}
