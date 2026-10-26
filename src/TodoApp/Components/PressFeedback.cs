using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Hosting;

namespace ReactorTodo.Components;

/// <summary>
/// Press feedback for an icon button: the *icon* shrinks while the button is held and springs back
/// on release, so it reads as "something got pushed in" while the button surface itself stays put.
///
/// The scale rides the compositor's <c>Visual.Scale</c> of the button content rather than a XAML
/// <c>RenderTransform</c>: a RenderTransform re-rasterises the glyph at a fractional scale for the
/// whole animation, which is what made the theme icon look blurry earlier. A compositor scale runs
/// off the UI thread and never touches layout.
/// </summary>
internal static class PressFeedback
{
    /// <summary>How far the icon dips while pressed.</summary>
    private const float PressedScale = 0.8f;

    private static readonly TimeSpan PressDuration = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan ReleaseDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>
    /// Adds press-to-scale feedback for the icon inside a button. Pointer events are taken from the
    /// whole button (so pressing anywhere in it counts) but only the content is scaled.
    /// </summary>
    public static ButtonElement WithIconPressScale(this ButtonElement button) => button.Set(OnCreated);

    private static void OnCreated(Button button)
    {

        // Set(...) runs while the element is being built, i.e. before Content is assigned, so the
        // wiring waits for Loaded.
        if (button.IsLoaded)
        {
            Wire(button);
            return;
        }

        RoutedEventHandler? onLoaded = null;
        onLoaded = (_, _) =>
        {
            button.Loaded -= onLoaded;
            Wire(button);
        };

        button.Loaded += onLoaded;
    }

    private static void Wire(Button button)
    {
        if (button.Content is not FrameworkElement icon)
        {
            return;
        }

        var iconVisual = ElementCompositionPreview.GetElementVisual(icon);
        if (iconVisual is null)
        {
            return;
        }

        // Scale around the icon centre so it shrinks in place.
        void Centre()
            => iconVisual.CenterPoint = new Vector3(
                (float)icon.ActualWidth / 2f,
                (float)icon.ActualHeight / 2f,
                0f);

        Centre();
        icon.SizeChanged += (_, _) => Centre();

        var compositor = iconVisual.Compositor;
        var press = Scale(compositor, PressedScale, PressDuration);
        var release = Scale(compositor, 1f, ReleaseDuration);

        // ButtonBase marks the pointer events as handled, so plain "+=" handlers never fire;
        // handledEventsToo: true is what actually delivers them.
        void OnPressed(object sender, PointerRoutedEventArgs args)
        {
            iconVisual.StartAnimation("Scale", press);
        }

        void OnReleased(object sender, PointerRoutedEventArgs args)
        {
            iconVisual.StartAnimation("Scale", release);
        }

        button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPressed), true);
        button.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnReleased), true);
        // Leaving the button or losing capture also ends the press, so the icon never stays shrunk.
        button.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler(OnReleased), true);
        button.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnReleased), true);
    }

    /// <summary>
    /// <c>Visual.Scale</c> is a Vector3 in WinUI 3 (X, Y and Z). Animating it with a Vector2
    /// keyframe throws "expression output does not match animating property type".
    /// </summary>
    private static Vector3KeyFrameAnimation Scale(Compositor compositor, float target, TimeSpan duration)
    {
        var animation = compositor.CreateVector3KeyFrameAnimation();
        animation.InsertKeyFrame(
            1f,
            new Vector3(target, target, 1f),
            compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f)));
        animation.Duration = duration;
        return animation;
    }

}
