using Microsoft.UI.Reactor.Localization;

namespace ReactorTodo.Localization;

/// <summary>Short helpers so call sites read as <c>intl.T(StringKeys.AddButton)</c>.</summary>
internal static class IntlExtensions
{
    public static string T(this IntlAccessor intl, string key)
        => intl.Message(new MessageKey(AppStringProvider.Namespace, key));

    public static string T(this IntlAccessor intl, string key, params (string Name, object Value)[] args)
        => intl.Message(
            new MessageKey(AppStringProvider.Namespace, key),
            args.ToDictionary(static arg => arg.Name, static arg => arg.Value));
}
