using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Windows.System;
using static Microsoft.UI.Reactor.Factories;
using ReactorTodo.Localization;

namespace ReactorTodo.Components;

public sealed record AddTodoBarProps(TodoCommands Commands);

/// <summary>
/// The "new todo" composer. It owns its text-box state locally — this is the whole point
/// of hooks: no ViewModel, no binding, and typing only re-renders this component.
/// Strings come from <c>UseIntl()</c>, so the bar re-renders when the language changes.
/// </summary>
public sealed class AddTodoBar : Component<AddTodoBarProps>
{
    public override Element Render()
    {
        // Hooks are always called in the same order, unconditionally.
        var (draft, setDraft) = UseState("");
        var intl = UseIntl();

        var canAdd = !string.IsNullOrWhiteSpace(draft);

        void Submit()
        {
            if (!canAdd)
            {
                return;
            }

            Props.Commands.Add(draft);
            setDraft("");
        }

        return FlexRow(
                TextBox(draft, setDraft, placeholderText: intl.T(StringKeys.AddPlaceholder))
                    .AutomationName(intl.T(StringKeys.AutomationNewTodo))
                    .OnKeyDown((_, e) =>
                    {
                        if (e.Key == VirtualKey.Enter)
                        {
                            Submit();
                            e.Handled = true;
                        }
                    })
                    .Flex(grow: 1, basis: 0),
                // Primary action -> accent fill; disabled until there is something to add.
                Buttons.Primary(intl.T(StringKeys.AddButton), Submit)
                    .IsEnabled(canAdd)
                    .Flex(shrink: 0))
            with { ColumnGap = 8 };
    }
}
