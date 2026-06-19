using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;

namespace NSchema.Commands.Init;

/// <summary>
/// Scaffolds a starter NSchema project.
/// </summary>
internal sealed class ProjectScaffolder
{
    private const string ConfigFileName = "config.sql";
    private const string EnvironmentOverlayFileName = "config.env.prod.sql";
    private const string SchemaDirectoryName = "schemas";

    private const string EnvironmentOverlayTemplate =
        """
        -- Overlay for the 'prod' environment. Select it with:
        --   nschema plan --environment prod
        -- Any base block you don't override here still applies.

        BACKEND file (
          path = './nschema.prod.state.json'
        );

        """;

    // The project's provider/state configuration, declared as DDL config blocks. Config blocks may live in any .sql
    // file; a dedicated config.sql keeps them separate from the schema objects.
    private const string ConfigTemplate =
        """
        -- NSchema project configuration. These blocks tell NSchema which database to
        -- connect to and where to keep state. Config blocks may live in any .sql file.

        PROVIDER postgres (
          -- Prefer the NSCHEMA_POSTGRES_CONNECTION_STRING environment variable, which
          -- overrides the value below.
          connection_string = ''
        );

        BACKEND file (
          path = './nschema.state.json'
        );

        """;

    /// <summary>
    /// Writes the starter files into <paramref name="directory"/>, returning the created paths (relative to it).
    /// </summary>
    /// <param name="directory">The directory to scaffold into.</param>
    /// <param name="force">Force the initialization even if the directory is not empty.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">The directory is not empty, and <paramref name="force"/> is false.</exception>
    public static async Task<IReadOnlyList<string>> Scaffold(string directory, bool force, CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(directory, ConfigFileName);
        if (Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Length != 0 && !force)
        {
            throw new InvalidOperationException($"{directory} is not empty. Use --force to override.");
        }

        // This is sort of undermines the concept of having schema serializers,
        // but the parser ignores comments, so it's not very helpful for outputting instructive boilerplate.
        await File.WriteAllTextAsync(configPath, ConfigTemplate, cancellationToken);

        var overlayPath = Path.Combine(directory, EnvironmentOverlayFileName);
        await File.WriteAllTextAsync(overlayPath, EnvironmentOverlayTemplate, cancellationToken);

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, "example.sql");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);

        var ddl = DdlWriter.Instance.Write(SampleSchema);
        await File.WriteAllTextAsync(samplePath, ddl, cancellationToken);

        return [ConfigFileName, EnvironmentOverlayFileName, sampleRelativePath];
    }

    private static DatabaseSchema SampleSchema { get; } = new DatabaseSchema(
    [
        new SchemaDefinition("app", Tables:
        [
            new Table(
                "widgets",
                PrimaryKey: new PrimaryKey("widgets_pkey", ["id"]),
                Columns:
                [
                    new Column("id", SqlType.BigInt),
                    new Column("name", SqlType.Text, IsNullable: true),
                ]),
        ]),
    ]);
}
