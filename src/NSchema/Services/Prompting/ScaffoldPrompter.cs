using NSchema.Plugins;
using Spectre.Console;

namespace NSchema.Services.Prompting;

/// <summary>
/// Answers a plugin's scaffolding questions: from the values supplied on the command line where given, from the
/// operator where a terminal allows, and from the question's own default otherwise.
/// </summary>
/// <remarks>
/// A plugin declares what to ask; this decides how. Nothing here knows what any particular answer means, so a plugin
/// adding a question needs no change to the CLI.
/// </remarks>
internal static class ScaffoldPrompter
{
    /// <summary>
    /// Answers <paramref name="prompts"/>, preferring <paramref name="supplied"/> so a scripted run never blocks.
    /// </summary>
    /// <param name="console">The console to ask on, when it can be asked.</param>
    /// <param name="prompts">The questions the plugin declared, in the order to put them.</param>
    /// <param name="supplied">Answers given up front (from <c>--set</c>), keyed by prompt.</param>
    /// <returns>
    /// The answers, or a failure when a question with no default went unanswered and there is no terminal to ask on.
    /// </returns>
    public static Result<IReadOnlyDictionary<string, string?>> Answer(
        IAnsiConsole console,
        IReadOnlyList<ScaffoldPrompt> prompts,
        IReadOnlyDictionary<string, string?> supplied
    )
    {
        var answers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var unanswerable = new List<ScaffoldPrompt>();

        foreach (var prompt in prompts)
        {
            if (supplied.TryGetValue(prompt.Key, out var given) && !string.IsNullOrWhiteSpace(given))
            {
                answers[prompt.Key] = given;
                continue;
            }

            // Without a terminal (a redirected stdin, CI, a container) the default is the only answer available.
            if (!console.Profile.Capabilities.Interactive)
            {
                if (prompt.IsRequired)
                {
                    unanswerable.Add(prompt);
                }
                else
                {
                    answers[prompt.Key] = prompt.Default;
                }

                continue;
            }

            answers[prompt.Key] = Ask(console, prompt);
        }

        if (unanswerable.Count > 0)
        {
            // Failing here is deliberate: a scripted run that silently scaffolded a half-configured project would only
            // surface the problem at the first plan, further from the cause.
            var keys = string.Join(", ", unanswerable.Select(prompt => prompt.Key));
            return Result.Failure<IReadOnlyDictionary<string, string?>>(Diagnostic.Error("scaffold",
                $"No terminal to prompt on, and no value given for: {keys}. Supply each with --set <key>=<value>, or run interactively."));
        }

        return Result.Success<IReadOnlyDictionary<string, string?>>(answers);
    }

    private static string Ask(IAnsiConsole console, ScaffoldPrompt prompt)
    {
        if (prompt.Choices.Count > 0)
        {
            return console.Prompt(new SelectionPrompt<string>().Title(prompt.Label).AddChoices(prompt.Choices));
        }

        var text = new TextPrompt<string>(prompt.Label);
        if (prompt.IsSecret)
        {
            text = text.Secret();
        }

        // A question with a default may be accepted with a bare Enter; one without has to be answered.
        return prompt.Default is { } fallback ? console.Prompt(text.DefaultValue(fallback)) : console.Prompt(text);
    }
}
