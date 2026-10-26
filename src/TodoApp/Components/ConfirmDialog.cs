using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace ReactorTodo.Components;

/// <summary>
/// A reusable confirmation prompt for destructive actions.
///
/// The two buttons are rendered <b>inside the dialog content</b> instead of using
/// <c>PrimaryButtonText</c>/<c>SecondaryButtonText</c>: WinUI's built-in dialog button
/// slots only take text, so they cannot carry the danger styling from
/// <see cref="Buttons.DangerOutlined"/>. The dialog is fully state-driven — the caller
/// flips <c>IsOpen</c> through the reducer, and every way of closing it (Esc, the close
/// box, the buttons) lands in <c>OnClosed</c>. All text is passed in already localized.
/// </summary>
internal static class ConfirmDialog
{
    public static ContentDialogElement Create(
        bool isOpen,
        string title,
        string message,
        string warning,
        string confirmText,
        string cancelText,
        Action onConfirm,
        Action onCancel)
        => ContentDialog(
                title,
                VStack(
                    12,
                    TextBlock(message).TextWrapping(TextWrapping.Wrap),
                    Caption($"⚠ {warning}").Foreground(Theme.SystemCritical),
                    HStack(
                        8,
                        Buttons.Normal(cancelText, onCancel),
                        Buttons.DangerOutlined(confirmText, onConfirm))
                    with { HorizontalAlignment = HorizontalAlignment.Right }),

                // Empty primary text hides the built-in primary button on purpose;
                // the styled button pair above replaces it.
                primaryButtonText: "")
            with
            {
                IsOpen = isOpen,
                // Cancel is idempotent, so a close that follows a confirm is harmless.
                OnClosed = _ => onCancel(),
            };
}
