using EditorConfig.Core;

namespace NSchema.Configuration;

/// <summary>
/// Reads the diagnostic severities a project's <c>.editorconfig</c> sets.
/// </summary>
/// <remarks>
/// <para>
/// Resolution is the format's own: the chain is walked from each schema file up to the <c>root = true</c> file, and
/// the last matching section wins. That is done per schema file, because that is the only way the globs mean what
/// they say — and then the results are required to agree.
/// </para>
/// <para>
/// They have to agree because enforcement is applied to the <em>run</em>: the engine's findings are overwhelmingly
/// derived from the model rather than from source text, so all but a handful carry no file to resolve a section
/// against. A severity set in a section narrower than the schema is therefore refused rather than quietly widened
/// to the whole run, which is the reading a section like <c>[legacy/*.sql]</c> visibly promises and would not get.
/// Formatting keys, when they land, are per-file and have no such constraint.
/// </para>
/// </remarks>
internal static class EditorConfigReader
{
    /// <summary>
    /// Reads the overrides that apply to the project rooted at <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The project directory.</param>
    /// <param name="environment">The selected environment, whose overlay files join the base set, or <see langword="null"/>.</param>
    public static Result<DiagnosticOverrides> Read(string root, string? environment)
    {
        var files = SchemaFiles(root, environment);
        if (files.Length == 0)
        {
            return DiagnosticOverrides.None;
        }

        FileConfiguration[] resolved;
        try
        {
            resolved = [.. new EditorConfigParser().Parse(files)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            return Result.From(DiagnosticOverrides.None, [EditorConfigDiagnostics.Unreadable(ex.Message)]);
        }

        return Collect(resolved);
    }

    private static Result<DiagnosticOverrides> Collect(IReadOnlyCollection<FileConfiguration> resolved)
    {
        var diagnostics = new DiagnosticCollection();
        var byCode = new Dictionary<DiagnosticCode, PolicyEnforcement>();
        var bySource = new Dictionary<DiagnosticSource, PolicyEnforcement>();

        var keys = resolved
            .SelectMany(configuration => configuration.Properties.Keys)
            .Where(EditorConfigKeys.IsSeverityKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal);

        foreach (var key in keys)
        {
            // A file the key does not resolve for contributes null, so a section narrower than the schema disagrees
            // with the files it does not match rather than looking unanimous.
            if (resolved.Select(configuration => Value(configuration, key)).Distinct().ToArray() is not [{ } value])
            {
                diagnostics.Add(EditorConfigDiagnostics.ScopedSeverity(key));
                continue;
            }

            if (!EditorConfigKeys.TryParseSeverity(value, out var enforcement))
            {
                diagnostics.Add(EditorConfigDiagnostics.UnknownSeverity(key, value));
                continue;
            }

            if (enforcement is { } applied)
            {
                Apply(key, applied, byCode, bySource, diagnostics);
            }
        }

        return Result.From(new DiagnosticOverrides(byCode, bySource), diagnostics);
    }

    // The format lowercases keys but leaves values as written, so the severity word is folded here.
    private static string? Value(FileConfiguration configuration, string key) =>
        configuration.Properties.TryGetValue(key, out var value) ? value.Trim().ToLowerInvariant() : null;

    private static void Apply(string key, PolicyEnforcement enforcement,
        Dictionary<DiagnosticCode, PolicyEnforcement> byCode,
        Dictionary<DiagnosticSource, PolicyEnforcement> bySource,
        DiagnosticCollection diagnostics
    )
    {
        var isCode = key.StartsWith(EditorConfigKeys.CodePrefix, StringComparison.Ordinal);
        var name = EditorConfigKeys.NameOf(key, isCode ? EditorConfigKeys.CodePrefix : EditorConfigKeys.SourcePrefix);

        try
        {
            if (isCode)
            {
                byCode[new DiagnosticCode(name)] = enforcement;
            }
            else
            {
                bySource[new DiagnosticSource(name)] = enforcement;
            }
        }
        catch (ArgumentException ex)
        {
            diagnostics.Add(EditorConfigDiagnostics.InvalidDiagnosticName(key, Reason(ex)));
        }
    }

    // The validating parameter name is an implementation detail of a constructor the user never sees named.
    private static string Reason(ArgumentException ex) => ex.Message.Split(" (Parameter")[0];

    private static string[] SchemaFiles(string root, string? environment)
    {
        var files = ProjectGlobs.Match(root, ProjectGlobs.Base()).AsEnumerable();
        if (environment is not null)
        {
            files = files.Concat(ProjectGlobs.Match(root, ProjectGlobs.Environment(environment)));
        }

        return files.ToArray();
    }
}
