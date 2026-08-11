namespace NSchema.Configuration;

/// <summary>
/// How a project's <c>.editorconfig</c> asks for the engine's findings to be enforced.
/// </summary>
/// <param name="ByCode">The enforcement set for individual findings, by code.</param>
/// <param name="BySource">The enforcement set for every finding from a producer, by source.</param>
internal sealed record DiagnosticOverrides(
    IReadOnlyDictionary<DiagnosticCode, PolicyEnforcement> ByCode,
    IReadOnlyDictionary<DiagnosticSource, PolicyEnforcement> BySource
)
{
    /// <summary>
    /// No overrides: every finding is enforced as its producer reported it.
    /// </summary>
    public static DiagnosticOverrides None { get; } = new(
        new Dictionary<DiagnosticCode, PolicyEnforcement>(),
        new Dictionary<DiagnosticSource, PolicyEnforcement>());
}
