using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Cli.Configuration.Binding;

namespace NSchema.Cli.Configuration.Schema;

/// <summary>
/// Configures how the desired schema is located and read. Required for the plan and apply commands.
/// </summary>
internal sealed class SchemaConfig : IBindable
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

    public void Bind(ParseResult result)
    {
        SchemaOptions.Format.Bind(result, f => Format = f);
        SchemaOptions.Directory.Bind(result, d => Directory = d);
        SchemaOptions.Pattern.Bind(result, p => Pattern = p);
    }
}
