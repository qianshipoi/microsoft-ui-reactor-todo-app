using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;
using ReactorTodo.Localization;
using ReactorTodo.State;

namespace ReactorTodo.Components;

public sealed record FilterBarProps(
    TodoCommands Commands,
    TodoFilter Selected,
    int AllCount,
    int ActiveCount,
    int CompletedCount);

/// <summary>
/// All / Active / Completed switch. A good example of a stateless child component:
/// it renders from props and reports intent back through the command facade.
/// The counts ride along in the localized label ("全部 3" / "All 3" / "すべて 3").
/// </summary>
public sealed class FilterBar : Component<FilterBarProps>
{
    public override Element Render()
    {
        var intl = UseIntl();

        var items = new[]
        {
            SelectorBarItem(intl.T(StringKeys.FilterAll, ("count", Props.AllCount))),
            SelectorBarItem(intl.T(StringKeys.FilterActive, ("count", Props.ActiveCount))),
            SelectorBarItem(intl.T(StringKeys.FilterCompleted, ("count", Props.CompletedCount))),
        };

        return SelectorBar(
            items,
            selectedIndex: (int)Props.Selected,
            onSelectedIndexChanged: index =>
            {
                if (index >= 0)
                {
                    Props.Commands.SetFilter((TodoFilter)index);
                }
            });
    }
}
