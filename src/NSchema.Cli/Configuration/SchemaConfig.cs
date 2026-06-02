using Microsoft.Extensions.Configuration;

namespace NSchema.Cli.Configuration;

/// <summary>
/// Configures how the desired schema is located and read. Required for the plan and apply commands.
/// </summary>
internal sealed class SchemaConfig
{
    /// <summary>
    /// The directory the desired-schema files are discovered under.
    /// </summary>
    [ConfigurationKeyName("dir")]
    public string? Directory { get; set; }

    /// <summary>
    /// The format the desired schema is expressed in.
    /// </summary>
    public SchemaFormat Format { get; set; } = SchemaFormat.Yaml;

    /// <summary>
    /// The glob matched within <see cref="Directory"/>. When null, the format's default glob is used.
    /// </summary>
    public string? Glob { get; set; }
}
