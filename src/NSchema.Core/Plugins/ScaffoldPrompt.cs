namespace NSchema.Plugins;

/// <summary>
/// One question a plugin asks while a new project is being scaffolded.
/// </summary>
/// <remarks>
/// A prompt is not a setting. A plugin is free to ask for a host, a port and a database and compose them into the one
/// <c>connection_string</c> its statement carries — what to ask is the plugin's knowledge, and how to ask is the
/// front-end's, which is why the answers come back keyed by <see cref="Key"/> rather than by setting name.
/// </remarks>
public sealed record ScaffoldPrompt
{
    /// <summary>
    /// Identifies this prompt's answer in <see cref="ScaffoldContext.Answers"/>. Also the name a non-interactive
    /// front-end accepts the answer under, so it should read as an option would.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The question, as put to the user.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The value to use when the question goes unanswered, or <see langword="null"/> when an answer is required.
    /// </summary>
    public string? Default { get; init; }

    /// <summary>
    /// Whether the answer is a secret, and so should not be echoed, logged, or written into a project file.
    /// </summary>
    public bool IsSecret { get; init; }

    /// <summary>
    /// The permitted answers, when the question is a choice between known values; empty for free text.
    /// </summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// Whether an answer must be supplied — a prompt with no default cannot be skipped.
    /// </summary>
    public bool IsRequired => Default is null;
}
