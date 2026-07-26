using System.Diagnostics.CodeAnalysis;

namespace NSchema.Plugins;

/// <summary>
/// Describes the context in which a plugin's scaffold template is requested.
/// </summary>
public sealed record ScaffoldContext
{
    /// <summary>
    /// The environment the fragment is being scaffolded for, or <see langword="null"/> for the base configuration.
    /// </summary>
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// The answers to the prompts this plugin asked, keyed by <see cref="ScaffoldPrompt.Key"/>. Empty when the
    /// front-end asked nothing, in which case the plugin scaffolds the placeholders a user edits by hand.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Answers { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The answer to <paramref name="key"/>, or <paramref name="fallback"/> when it went unanswered.
    /// </summary>
    [return: NotNullIfNotNull(nameof(fallback))]
    public string? Answer(string key, string? fallback = null) =>
        Answers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;
}
