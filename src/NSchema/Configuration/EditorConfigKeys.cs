namespace NSchema.Configuration;

/// <summary>
/// The <c>.editorconfig</c> keys NSchema reads, and the severity vocabulary they take.
/// </summary>
/// <remarks>
/// The spelling deliberately echoes Roslyn's <c>dotnet_diagnostic.&lt;id&gt;.severity</c>.
/// The severity words are Roslyn's too, so that a value copied from a C# section means what it looks like it means.
/// </remarks>
internal static class EditorConfigKeys
{
    /// <summary>
    /// The prefix of a key configuring one finding, by its code: <c>nschema_diagnostic.&lt;code&gt;.severity</c>.
    /// </summary>
    public const string CodePrefix = "nschema_diagnostic.";

    /// <summary>
    /// The prefix of a key configuring every finding from one producer, by its source:
    /// <c>nschema_diagnostic_source.&lt;source&gt;.severity</c>. Spelled out rather than folded into
    /// <see cref="CodePrefix"/> as a <c>source-</c> qualifier, which a code of that name would collide with.
    /// </summary>
    public const string SourcePrefix = "nschema_diagnostic_source.";

    /// <summary>
    /// The suffix every severity key ends with.
    /// </summary>
    public const string SeveritySuffix = ".severity";

    /// <summary>
    /// The severity values, for a message listing them.
    /// </summary>
    public const string Severities = "none, silent, suggestion, warning, error, or default";

    /// <summary>
    /// Whether <paramref name="key"/> is one of the severity keys, whichever kind.
    /// </summary>
    public static bool IsSeverityKey(string key) =>
        key.EndsWith(SeveritySuffix, StringComparison.Ordinal)
        && (key.StartsWith(CodePrefix, StringComparison.Ordinal) || key.StartsWith(SourcePrefix, StringComparison.Ordinal));

    /// <summary>
    /// The name a severity key configures — the code or the source — given the prefix it carries.
    /// </summary>
    public static string NameOf(string key, string prefix) =>
        key[prefix.Length..^SeveritySuffix.Length];

    /// <summary>
    /// The enforcement a severity value asks for: <see langword="null"/> when the value leaves the finding as its
    /// producer reported it (<c>default</c>), and <see langword="false"/> when it is not a severity at all.
    /// </summary>
    public static bool TryParseSeverity(string value, out PolicyEnforcement? enforcement)
    {
        enforcement = value switch
        {
            "none" or "silent" => PolicyEnforcement.Ignore,
            "suggestion" or "info" => PolicyEnforcement.Allow,
            "warning" => PolicyEnforcement.Warn,
            "error" => PolicyEnforcement.Error,
            _ => null,
        };

        return enforcement is not null || value == "default";
    }
}
