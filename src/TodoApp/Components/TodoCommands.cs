using ReactorTodo.State;

namespace ReactorTodo.Components;

/// <summary>
/// A stable facade over <c>dispatch</c>. Because one instance is created with
/// <c>UseMemo</c> it keeps the same reference across renders, so row props stay
/// structurally equal and untouched rows skip re-rendering.
/// </summary>
public sealed class TodoCommands(Action<TodoAction> dispatch)
{
    public void Add(string title) => dispatch(new AddTodo(title));

    public void SetDone(string id, bool done) => dispatch(new SetTodoDone(id, done));

    public void Rename(string id, string title) => dispatch(new RenameTodo(id, title));

    /// <summary>Asks for confirmation before deleting a row.</summary>
    public void RequestDelete(string id) => dispatch(new RequestDelete(id));

    public void CancelDelete() => dispatch(new CancelDelete());

    public void ConfirmDelete() => dispatch(new ConfirmDelete());

    /// <summary>Asks for confirmation before removing the completed rows.</summary>
    public void RequestClearCompleted() => dispatch(new RequestClearCompleted());

    public void CancelClearCompleted() => dispatch(new CancelClearCompleted());

    public void ConfirmClearCompleted() => dispatch(new ConfirmClearCompleted());

    public void SetFilter(TodoFilter filter) => dispatch(new SetFilter(filter));
}
