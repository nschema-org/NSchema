using System.Text.Json;
using NSchema.Configuration;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;
using NSchema.Resolution;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;
using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Commands.Init;

/// <summary>
/// Scaffolds a starter NSchema project — an <c>nschema.json</c> and a sample schema file — into a directory.
/// </summary>
internal sealed class ProjectScaffolder
{
    private const string ConfigFileName = "nschema.json";
    private const string SchemaDirectoryName = "schemas";

    /// <summary>
    /// Writes the starter files into <paramref name="directory"/>, returning the created paths (relative to it).
    /// </summary>
    /// <param name="directory">The directory to scaffold into.</param>
    /// <param name="force">Whether to overwrite an existing <c>nschema.json</c>.</param>
    /// <param name="serializers">Resolves the schema serializer used to write the sample schema.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">An <c>nschema.json</c> already exists and <paramref name="force"/> is false.</exception>
    public static async Task<IReadOnlyList<string>> Scaffold(
        string directory,
        bool force,
        IKeyedResolver<ISchemaSerializer> serializers,
        CancellationToken cancellationToken = default
    )
    {
        var configPath = Path.Combine(directory, ConfigFileName);
        if (File.Exists(configPath) && !force)
        {
            throw new InvalidOperationException($"{ConfigFileName} already exists. Use --force to overwrite.");
        }

        var config = new ScaffoldedProjectConfig
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig() },
            State = new StateConfig { File = new FileStateConfig { Path = "./nschema.state.json" } },
        };
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, ConfigurationFactory.JsonOptions), cancellationToken);

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, "example.sql");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);

        var serializer = serializers.Resolve(DdlSchemaSerializer.FormatName);
        await using var stream = File.Create(samplePath);
        await serializer.Write(SampleSchema, stream, cancellationToken);

        return [ConfigFileName, sampleRelativePath];
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
