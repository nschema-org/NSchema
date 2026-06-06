using System.Text.Json;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;
using NSchema.Resolution;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Cli.Commands.Init;

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
    /// <param name="format">The format to write the sample schema in.</param>
    /// <param name="force">Whether to overwrite an existing <c>nschema.json</c>.</param>
    /// <param name="serializers">Resolves the schema serializer for the chosen <paramref name="format"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">An <c>nschema.json</c> already exists and <paramref name="force"/> is false.</exception>
    public async Task<IReadOnlyList<string>> Scaffold(
        string directory,
        SchemaFormat format,
        bool force,
        IKeyedResolver<ISchemaDocumentSerializer> serializers,
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
            Schema = new SchemaConfig { Directory = $"./{SchemaDirectoryName}", Format = format },
        };
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, ConfigurationFactory.JsonOptions), cancellationToken);

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, $"example.{format.Extension()}");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);

        var serializer = serializers.Resolve(format.FormatName());
        await using var stream = File.Create(samplePath);
        await serializer.Write(SampleSchema, stream, cancellationToken);

        return [ConfigFileName, sampleRelativePath];
    }

    private static DatabaseSchema SampleSchema { get; } = DatabaseSchema.Create(
    [
        SchemaDefinition.Create("app", tables:
        [
            Table.Create(
                "widgets",
                primaryKey: new PrimaryKey("widgets_pkey", ["id"]),
                columns:
                [
                    Column.Create("id", SqlType.BigInt),
                    Column.Create("name", SqlType.Text, isNullable: true),
                ]),
        ]),
    ]);
}
