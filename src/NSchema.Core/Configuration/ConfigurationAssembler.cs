using NSchema.Configuration.Engine;
using NSchema.Configuration.Plugins;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Configuration;

/// <summary>
/// Assembles parsed configuration documents into a <see cref="ConfigurationDefinition"/>, validating as it goes.
/// </summary>
internal static class ConfigurationAssembler
{
    // The labels Core itself serves (the built-in file state store), which no PLUGIN statement declares.
    private static readonly HashSet<PluginLabel> _builtInLabels = [new("file")];

    /// <summary>
    /// Validates and resolves <paramref name="documents"/> into the configuration they declare.
    /// </summary>
    /// <param name="documents">The configuration documents resolved together; only their settings statements are bound.</param>
    /// <param name="environment">The environment overrides to apply; the process environment when not supplied.</param>
    public static Result<ConfigurationDefinition, NsqlDiagnostic> Assemble(
        IReadOnlyList<NsqlDocument> documents,
        IReadOnlyDictionary<string, string?>? environment = null
    )
    {
        DiagnosticCollection<NsqlDiagnostic> diagnostics = [];

        // Roll the statements up by keyword, then resolve each keyword's group against its own rule. Plugins
        // resolve first so a reference declared before its PLUGIN still finds it.
        var byKeyword = documents
            .SelectMany(document => document.Statements.OfType<SettingsStatement>().Select(statement => new Located(statement, document.FilePath)))
            .ToLookup(located => located.Statement.Keyword);

        var plugins = Plugins(byKeyword[SettingsKeyword.Plugin], diagnostics);
        var definition = new ConfigurationDefinition(
            plugins,
            Sole(byKeyword[SettingsKeyword.Engine], NsqlKeywords.Engine, diagnostics)?.Bind<EngineConfiguration>(diagnostics),
            Reference(byKeyword[SettingsKeyword.Database], NsqlKeywords.Database, plugins, diagnostics, environment),
            Reference(byKeyword[SettingsKeyword.State], NsqlKeywords.State, plugins, diagnostics, environment));

        return diagnostics.ToResult(definition);
    }

    // A declaration collection: bind each, keeping only the first with a given label and the first with a given source.
    private static List<PluginDeclaration> Plugins(IEnumerable<Located> statements, DiagnosticCollection<NsqlDiagnostic> diagnostics)
    {
        var plugins = new List<PluginDeclaration>();
        foreach (var located in statements)
        {
            // The label is structural (the statement's identifier); only the attributes are bound.
            if (located.Bind<PluginOriginSettings>(diagnostics) is not { } settings || located.Statement.Label is not { } label)
            {
                continue;
            }

            if (Origin(settings, label.Value, located, diagnostics) is not { } origin)
            {
                continue;
            }

            var declaration = new PluginDeclaration(label.Value, origin);
            if (plugins.Any(p => p.Label == declaration.Label))
            {
                diagnostics.Add(located.Stamp(PluginDiagnostics.DuplicatePluginLabel(declaration.Label, located.Statement.Position)));
            }

            // Declaring one package twice is a mistake: it is declared once and referenced by label. Declaring one
            // path twice is not the same thing — a path names bits rather than an identity, and nothing keys on it.
            else if (declaration.Package is { } package && plugins.Any(p => p.Package?.Source == package.Source))
            {
                diagnostics.Add(located.Stamp(PluginDiagnostics.DuplicatePluginSource(package.Source, located.Statement.Position)));
            }
            else
            {
                plugins.Add(declaration);
            }
        }

        return plugins;
    }

    // Which origin the author reached for. Binding cannot decide this, because what is required depends on the
    // answer, so the combination is judged here where a diagnostic can name it.
    private static PluginOrigin? Origin(
        PluginOriginSettings settings,
        PluginLabel label,
        Located located,
        DiagnosticCollection<NsqlDiagnostic> diagnostics
    )
    {
        var position = located.Statement.Position;

        if (settings.Path is { Length: > 0 } path)
        {
            // Rejected rather than ignored: a package attribute beside a path reads as if it pins something, and
            // it pins nothing. Which attribute it was does not change the answer, so neither does the diagnostic.
            if (settings.Source is not null || settings.Version is not null)
            {
                diagnostics.Add(located.Stamp(PluginDiagnostics.ConflictingPluginOrigin(label, position)));
                return null;
            }

            return new PathOrigin(path);
        }

        if (settings.Source is not { } source || settings.Version is not { } version)
        {
            diagnostics.Add(located.Stamp(PluginDiagnostics.MissingPluginOrigin(label, position)));
            return null;
        }

        return new PackageOrigin(new PackageReference { Source = source, Version = version });
    }

    // A provider reference (DATABASE/STATE): at most one, labelled, and resolving to a declared or built-in plugin.
    private static PluginSettings? Reference(
        IEnumerable<Located> statements,
        string keyword,
        IReadOnlyList<PluginDeclaration> plugins,
        DiagnosticCollection<NsqlDiagnostic> diagnostics,
        IReadOnlyDictionary<string, string?>? environment
    )
    {
        if (Sole(statements, keyword, diagnostics) is not { } located)
        {
            return null;
        }

        if (located.Statement.Label is not { } label)
        {
            diagnostics.Add(located.Stamp(ConfigurationDiagnostics.UnlabelledReference(keyword, located.Statement.Position)));
            return null;
        }

        PluginLabel reference = label.Value;
        if (plugins.All(p => p.Label != reference) && !_builtInLabels.Contains(reference))
        {
            diagnostics.Add(located.Stamp(PluginDiagnostics.UnknownPluginLabel(keyword, label.Value, label.Position)));
            return null;
        }

        return EnvironmentSettings.Overlay(located.Statement.ToSettings(), keyword, environment);
    }

    // Enforces at-most-one for a keyword, returning the first and reporting each one beyond it as a duplicate.
    private static Located? Sole(IEnumerable<Located> statements, string keyword, DiagnosticCollection<NsqlDiagnostic> diagnostics)
    {
        Located? first = null;
        foreach (var located in statements)
        {
            if (first is null)
            {
                first = located;
            }
            else
            {
                diagnostics.Add(located.Stamp(ConfigurationDiagnostics.DuplicateStatement(keyword, located.Statement.Position)));
            }
        }

        return first;
    }

    // A parsed statement paired with the file it came from, so the diagnostics it raises can be attributed to it.
    private sealed record Located(SettingsStatement Statement, string? File)
    {
        // Binds the statement's attributes to a new T, attributing any binding diagnostics; null when binding fails.
        public T? Bind<T>(DiagnosticCollection<NsqlDiagnostic> diagnostics) where T : notnull
        {
            var result = Statement.ToSettings().Get<T>();
            diagnostics.AddRange(result.Diagnostics.Select(d => Stamp(new NsqlDiagnostic(d.Source, d.Code, d.Text, d.Severity, Statement.Position))));
            return result.IsSuccess ? result.Require() : default;
        }

        // Attributes a diagnostic to this statement's file.
        public NsqlDiagnostic Stamp(NsqlDiagnostic diagnostic) => diagnostic with { File = File };
    }
}
