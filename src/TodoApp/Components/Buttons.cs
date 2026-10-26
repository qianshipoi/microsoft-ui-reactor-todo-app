using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

namespace ReactorTodo.Components;

/// <summary>
/// Button roles, so intent is readable at the call site.
///
/// WinUI ships several button *styles*: the default (neutral) one,
/// <c>AccentButtonStyle</c> (solid accent fill), <c>SubtleButtonStyle</c> (borderless,
/// fill only on hover) and text-link. What it does <b>not</b> ship is a destructive
/// style, so <see cref="Danger"/> and <see cref="DangerOutlined"/> build one from theme
/// tokens instead of hardcoded colors — they keep working in light, dark and
/// high-contrast themes.
///
/// Recoloring a button is done with lightweight styling: the WinUI resource keys below
/// (<c>ButtonBackground</c>, <c>ButtonBackgroundPointerOver</c>, …) are read by the
/// button template, so overriding them also covers hover and pressed states.
/// </summary>
internal static class Buttons
{
    /// <summary>
    /// Corner radius every WinUI button uses. The destructive roles below must set it
    /// explicitly: <c>.Foreground(...)</c> swaps the element onto a plain Button style,
    /// and that style carries no CornerRadius setter, so the control ends up with
    /// 0,0,0,0 — square corners on an otherwise system-shaped button. Reading the same
    /// theme resource the default style uses keeps the two in sync (also in high contrast).
    /// </summary>
    private static CornerRadius ControlCornerRadius => ThemeResource.CornerRadius("ControlCornerRadius");

    /// <summary>Primary action: solid accent fill (WinUI accent style).</summary>
    public static ButtonElement Primary(string label, Action onClick)
        => Button(label, onClick).AccentButton().AutomationName(label);

    /// <summary>Neutral action: the default WinUI button.</summary>
    public static ButtonElement Normal(string label, Action onClick)
        => Button(label, onClick).AutomationName(label);

    /// <summary>Low emphasis action: borderless until hovered (WinUI subtle style).</summary>
    public static ButtonElement Subtle(string label, Action onClick)
        => Button(label, onClick).SubtleButton().AutomationName(label);

    /// <summary>
    /// Destructive action at rest weight: red text, no fill until hovered — the same
    /// treatment WinUI uses for destructive menu items. Right for a repeatable row action.
    /// </summary>
    public static ButtonElement Danger(string label, Action onClick)
        => Button(label, onClick)
            .SubtleButton()
            .Foreground(Theme.SystemCritical)
            .CornerRadius(ControlCornerRadius)
            .AutomationName(label)
            .Resources(r => r
                .Set("ButtonForegroundPointerOver", Theme.SystemCritical)
                .Set("ButtonForegroundPressed", Theme.SystemCritical)
                .Set("ButtonBackgroundPointerOver", Theme.SystemCriticalBackground)
                .Set("ButtonBackgroundPressed", Theme.SystemCriticalBackground));

    /// <summary>
    /// Destructive action that ends the flow (a dialog's confirm button). It keeps the
    /// <b>default</b> button frame — same corner radius, padding and height as the neutral
    /// button next to it — and signals danger with color only: red text and a red outline.
    /// No solid fill, so the pair reads as two native WinUI buttons.
    /// </summary>
    public static ButtonElement DangerOutlined(string label, Action onClick)
        => Button(label, onClick)
            .Foreground(Theme.SystemCritical)
            .CornerRadius(ControlCornerRadius)
            .AutomationName(label)
            .Resources(r => r
                .Set("ButtonBackground", Theme.Ref("ControlFillColorTransparentBrush"))
                .Set("ButtonBackgroundPointerOver", Theme.SystemCriticalBackground)
                .Set("ButtonBackgroundPressed", Theme.SystemCriticalBackground)
                .Set("ButtonForegroundPointerOver", Theme.SystemCritical)
                .Set("ButtonForegroundPressed", Theme.SystemCritical)
                .Set("ButtonBorderBrush", Theme.SystemCritical)
                .Set("ButtonBorderBrushPointerOver", Theme.SystemCritical)
                .Set("ButtonBorderBrushPressed", Theme.SystemCritical));
}

