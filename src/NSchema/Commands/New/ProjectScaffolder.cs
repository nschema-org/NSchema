using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Settings;

namespace NSchema.Commands.New;

/// <summary>
/// Composes a starter NSchema project from caller-supplied config statements and a sample schema, writing the files
/// to disk. It is deliberately pure plumbing: the DATABASE/STATE statements and the sample schema come from the
/// plugins (resolved by the command), so the CLI itself owns only the file framing, the <c>PLUGIN</c> declarations
/// (it knows the package and resolved version), and the built-in local-file state store, which has no plugin.
/// </summary>
internal static class ProjectScaffolder
{
    private const string ConfigurationFileName = "config.nsql";
    private const string EnvironmentOverlayFileName = "config.env.prod.nsql";
    private const string SchemaDirectoryName = "schemas";

    private const string ConfigurationHeader =
        """
        -- NSchema project configuration. These statements declare the engine version the
        -- project needs, the plugins it depends on, which database to connect to, and where
        -- to keep state.
        """;

    private const string OverlayHeader =
        """
        -- Overlay for the 'prod' environment. Select it with:
        --   nschema plan --environment prod
        -- Any base statement you don't override here still applies.
        """;

    /// <summary>
    /// The built-in local-file state store's configuration. Every other backend is a plugin that renders its own.
    /// </summary>
    public static NsqlDocument FileState { get; } = FileStateDocument("./nschema.state.json");

    /// <summary>
    /// The built-in local-file state store's configuration for the scaffolded environment overlay.
    /// </summary>
    public static NsqlDocument FileStateOverlay { get; } = FileStateDocument("./nschema.prod.state.json");

    /// <summary>
    /// Writes <paramref name="template"/> into <paramref name="directory"/>, returning the created paths (relative
    /// to it).
    /// </summary>
    /// <param name="directory">The directory to scaffold into.</param>
    /// <param name="force">Force the scaffolding even if the directory is not empty.</param>
    /// <param name="template">What to scaffold: the declared plugins and the documents they contribute.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created paths, or a failure when the directory is not empty and <paramref name="force"/> is false.</returns>
    public static async Task<Result<IReadOnlyList<string>>> Scaffold(
        string directory,
        bool force,
        ProjectTemplate template,
        CancellationToken cancellationToken = default)
    {
        if (Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length != 0 && !force)
        {
            return ScaffoldDiagnostics.DirectoryNotEmpty(directory);
        }

        // Each contributor hands over a document; they are merged and written once, so nothing here builds NSQL. The
        // headers stay outside the documents: a file header introduces the file, not the statement it happens to
        // precede, and the language has no statement for one.
        var configuration = NsqlDocument.Concat(
        [
            new NsqlDocument([Engine(template.EngineRequirement), .. template.Plugins.Select(Plugin)]),
            template.Database,
            template.State,
        ]);

        await File.WriteAllTextAsync(Path.Combine(directory, ConfigurationFileName), Introduced(ConfigurationHeader, configuration), cancellationToken);
        var overlay = NsqlDocument.Concat([template.DatabaseOverlay, template.StateOverlay]);
        await File.WriteAllTextAsync(Path.Combine(directory, EnvironmentOverlayFileName), Introduced(OverlayHeader, overlay), cancellationToken);

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, "example.nsql");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);
        await File.WriteAllTextAsync(samplePath, NsqlWriter.Write(template.Schema), cancellationToken);

        return Result.Success<IReadOnlyList<string>>([ConfigurationFileName, EnvironmentOverlayFileName, sampleRelativePath]);
    }

    // The host authors the ENGINE assertion: the engine is compiled into the CLI, so it knows the version range a
    // project scaffolded now requires.
    private static SettingsStatement Engine(string engineRequirement) =>
        SettingsStatement.Engine().WithSetting("version", engineRequirement);

    // The host authors the PLUGIN statements: it resolved the packages, so it knows the pins.
    private static SettingsStatement Plugin(ResolvedPlugin plugin) =>
        SettingsStatement.Plugin(plugin.Label.Value)
            .WithSetting("source", plugin.Source.Value)
            .WithSetting("version", plugin.Version.ToString());

    // A file body: its header, a blank line, then the document.
    private static string Introduced(string header, NsqlDocument document) =>
        $"{header.TrimEnd()}\n\n{NsqlWriter.Write(document)}";

    // The local-file state store is built into the core, so — unlike every other backend — it has no plugin to render
    // its statement. The CLI owns it.
    private static NsqlDocument FileStateDocument(string path) =>
        new([SettingsStatement.State("file").WithSetting("path", path)]);
}
