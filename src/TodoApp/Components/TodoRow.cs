using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Windows.System;
using Windows.UI.Text;
using static Microsoft.UI.Reactor.Factories;
using ReactorTodo.Localization;
using ReactorTodo.Models;

namespace ReactorTodo.Components;

public sealed record TodoRowProps(TodoItem Item, TodoCommands Commands);

/// <summary>
/// One checklist row. It keeps its own "am I being edited?" state, so the parent list
/// never needs to know about edit mode, and the state stays attached to the item because
/// the row element is keyed by <see cref="TodoItem.Id"/> in the list.
/// </summary>
public sealed class TodoRow : Component<TodoRowProps>
{
    public override Element Render()
    {
        var (editing, setEditing) = UseState(false);
        var (draft, setDraft) = UseState("");
        var intl = UseIntl();

        var item = Props.Item;

        void BeginEdit()
        {
            setDraft(item.Title);
            setEditing(true);
        }

        void Commit()
        {
            setEditing(false);
            Props.Commands.Rename(item.Id, draft);
        }

        void Cancel() => setEditing(false);

        void ToggleDone() => Props.Commands.SetDone(item.Id, !item.Done);

        // alignSelf: Center keeps the text at its natural height and centres that box in the
        // row, instead of stretching it to the full row height with the glyphs at the top.
        //
        // View mode also makes the title a tap target: clicking the text toggles the item,
        // like clicking the checkbox. The transparent Background is what makes the whole
        // column hit-testable (a TextBlock alone only reacts on its glyphs), and edit mode
        // deliberately has no tap handler so clicking into the text box does not toggle.
        Element title = editing
            ? TextBox(draft, setDraft)
                .AutomationName(intl.T(StringKeys.AutomationEdit, ("title", item.Title)))
                .OnKeyDown((_, e) =>
                {
                    if (e.Key == VirtualKey.Enter)
                    {
                        Commit();
                        e.Handled = true;
                    }
                    else if (e.Key == VirtualKey.Escape)
                    {
                        Cancel();
                        e.Handled = true;
                    }
                })
                .Flex(grow: 1, basis: 0, alignSelf: FlexAlign.Center)
            : Border(
                    TextBlock(item.Title)
                        .TextWrapping(TextWrapping.Wrap)
                        .TextDecorations(item.Done ? TextDecorations.Strikethrough : TextDecorations.None)
                        .Foreground(item.Done ? Theme.SecondaryText : Theme.PrimaryText))
                .Background(Theme.Ref("ControlFillColorTransparentBrush"))
                .Padding(0, 4)
                .AutomationName(intl.T(StringKeys.AutomationToggle, ("title", item.Title)))
                .IsTabStop(true)
                .OnTapped((_, _) => ToggleDone())
                // Keyboard parity with the tap target: Space/Enter toggles like a checkbox.
                .OnKeyDown((_, e) =>
                {
                    if (e.Key == VirtualKey.Space || e.Key == VirtualKey.Enter)
                    {
                        ToggleDone();
                        e.Handled = true;
                    }
                })
                .Flex(grow: 1, basis: 0, alignSelf: FlexAlign.Center, minWidth: 0);

        // NOTE: the action group is an HStack (StackPanel), not a nested FlexRow.
        // A FlexPanel nested inside another FlexPanel collapses to zero size in Reactor
        // 0.1.0-preview.16, which leaves the row's checkbox and title unarranged.
        //
        // Button roles: editing keeps one emphasised (save) + one low-emphasis (cancel)
        // action; viewing keeps a low-emphasis edit and a destructive delete.
        Element actions = editing
            ? HStack(
                8,
                Buttons.Primary(intl.T(StringKeys.SaveButton), Commit),
                Buttons.Subtle(intl.T(StringKeys.CancelButton), Cancel))
            : HStack(
                8,
                Buttons.Subtle(intl.T(StringKeys.EditButton), BeginEdit),
                Buttons.Danger(intl.T(StringKeys.DeleteButton), () => Props.Commands.RequestDelete(item.Id)));

        return Border(
                FlexRow(
                        // WinUI's CheckBox style carries a 120px MinWidth, which leaves a huge
                        // empty gap after the box; collapse it and let the row drive the height.
                        CheckBox(item.Done, done => Props.Commands.SetDone(item.Id, done))
                            .AutomationName(intl.T(StringKeys.AutomationToggle, ("title", item.Title)))
                            .MinWidth(0)
                            .Flex(shrink: 0, alignSelf: FlexAlign.Center),
                        title,
                        actions.Flex(shrink: 0))
                    with { ColumnGap = 12 })
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .CornerRadius(6)
            .Padding(10, 6);
    }
}



