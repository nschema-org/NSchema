using System.Text.Json;
using NSchema.Cli.Configuration;

namespace NSchema.Cli;

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
    /// <exception cref="InvalidOperationException">An <c>nschema.json</c> already exists and <paramref name="force"/> is false.</exception>
    public IReadOnlyList<string> Scaffold(string directory, SchemaFormat format, bool force)
    {
        var configPath = Path.Combine(directory, ConfigFileName);
        if (File.Exists(configPath) && !force)
        {
            throw new InvalidOperationException($"{ConfigFileName} already exists. Use --force to overwrite.");
        }

        var config = new NSchemaConfiguration
        {
            Provider = new ProviderConfig { Postgres = new PostgresProviderConfig() },
            State = new StateConfig { File = new FileStateConfig { Path = "./nschema.state.json" } },
            Schema = new SchemaConfig { Directory = $"./{SchemaDirectoryName}", Format = format },
        };
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, NSchemaConfigurationFactory.JsonOptions));

        var sampleRelativePath = Path.Combine(SchemaDirectoryName, $"example.{format.Extension()}");
        var samplePath = Path.Combine(directory, sampleRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);
        File.WriteAllText(samplePath, SampleSchema(format));

        return [ConfigFileName, sampleRelativePath];
    }

    private static string SampleSchema(SchemaFormat format) => format switch
    {
        SchemaFormat.Yaml =>
            """
            schemas:
              - name: app
                tables:
                  - name: widgets
                    primaryKey:
                      name: widgets_pkey
                      columnNames: [id]
                    columns:
                      - name: id
                        type: bigint
                        isNullable: false
                      - name: name
                        type: text
                        isNullable: true

            """,
        SchemaFormat.Json =>
            """
            {
              "schemas": [
                {
                  "name": "app",
                  "tables": [
                    {
                      "name": "widgets",
                      "primaryKey": { "name": "widgets_pkey", "columnNames": ["id"] },
                      "columns": [
                        { "name": "id", "type": "bigint", "isNullable": false },
                        { "name": "name", "type": "text", "isNullable": true }
                      ]
                    }
                  ]
                }
              ]
            }

            """,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown schema format."),
    };
}
