using System.CommandLine;
using System.Text.Json.Serialization;

namespace NSchema.Cli.Configuration.Schema;

/// <summary>
/// Configures how the desired schema is located and read. Required for the plan and apply commands.
/// </summary>
internal sealed class SchemaConfig : IConfigurable
{
    /// <summary>
    /// The directory the desired-schema files are discovered under.
    /// </summary>
    [JsonPropertyName("dir")]
    public string Directory { get; set; } = "";

    /// <summary>
    /// The format the desired schema is expressed in.
    /// </summary>
    public SchemaFormat Format { get; set; } = SchemaFormat.Yaml;

    /// <summary>
    /// The glob matched within <see cref="Directory"/>. When null, the format's default glob is used.
    /// </summary>
    public string? Pattern { get; set; }

    public void Configure(ParseResult result)
    {
        if (SchemaOptions.Format.TryResolve(result, out var format))
        {
            Format = format;
        }

        if (SchemaOptions.Directory.TryResolve(result, out var directory))
        {
            Directory = directory;
        }

        if (SchemaOptions.Pattern.TryResolve(result, out var pattern))
        {
            Pattern = pattern;
        }
    }
}
