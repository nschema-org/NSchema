namespace NSchema.Configuration;

/// <summary>
/// How a project's <c>.editorconfig</c> asks for findings to be enforced.
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

    /// <summary>
    /// The same enforcement the engine applies to its own findings, for findings the engine never sees.
    /// </summary>
    /// <remarks>
    /// Reading a project produces findings of its own — a lockfile that pins no version, a provider loaded from a
    /// path — and they are minted before an engine exists to hold a policy. Reusing <see cref="DiagnosticOptions"/>
    /// is what keeps one setting meaning one thing across both: the precedence of code over source, and the refusal
    /// to lower a structural finding, stay decided in exactly one place.
    /// </remarks>
    public DiagnosticOptions ToOptions()
    {
        var options = new DiagnosticOptions();

        foreach (var (code, enforcement) in ByCode)
        {
            options.ByCode[code] = enforcement;
        }

        foreach (var (source, enforcement) in BySource)
        {
            options.BySource[source] = enforcement;
        }

        return options;
    }
}
