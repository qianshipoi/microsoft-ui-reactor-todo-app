namespace ReactorTodo.Models;

/// <summary>
/// One row in the todo list. A record gives us structural equality for free, which is
/// what lets Reactor skip re-rendering rows whose item did not change.
/// </summary>
public sealed record TodoItem(string Id, string Title, bool Done, DateTimeOffset CreatedAt)
{
    public static TodoItem Create(string title) =>
        new(Guid.NewGuid().ToString("n"), title.Trim(), false, DateTimeOffset.Now);
}
