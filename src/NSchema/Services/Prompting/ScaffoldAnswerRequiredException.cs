namespace NSchema.Services.Prompting;

/// <summary>
/// A plugin asked something that has no default, and there was no terminal to ask it on.
/// </summary>
/// <remarks>
/// Failing here is deliberate: a scripted run that silently scaffolded a half-configured project would only surface
/// the problem at the first plan, further from the cause.
/// </remarks>
internal sealed class ScaffoldAnswerRequiredException(IEnumerable<string> keys)
    : Exception(BuildMessage(keys))
{
    private static string BuildMessage(IEnumerable<string> keys) =>
        $"No terminal to prompt on, and no value given for: {string.Join(", ", keys)}. "
        + $"Supply each with --set <key>=<value>, or run interactively.";
}
