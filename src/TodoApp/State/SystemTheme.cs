using Windows.UI;
using Windows.UI.ViewManagement;

namespace ReactorTodo.State;

/// <summary>
/// Reads the Windows light/dark setting. Used by the "follow system" mode so the app tracks the
/// system's *light/dark* preference live — it deliberately ignores the user's custom accent or
/// theme colour, because the UI is built from neutral Theme.* tokens.
/// </summary>
internal static class SystemTheme
{
    /// <summary>True when the system background is dark.</summary>
    public static bool IsDark()
    {
        try
        {
            return Luminance(new UISettings().GetColorValue(UIColorType.Background)) < 128;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static double Luminance(Color color) => (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
}
