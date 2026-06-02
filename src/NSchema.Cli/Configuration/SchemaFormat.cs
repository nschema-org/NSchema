namespace NSchema.Cli.Configuration;

/// <summary>
/// The format the desired schema is expressed in. Selects which provider reads the schema files.
/// </summary>
internal enum SchemaFormat
{
    /// <summary>
    /// YAML schema files (the default).
    /// </summary>
    Yaml,

    /// <summary>
    /// JSON schema files.
    /// </summary>
    Json,
}

internal static class SchemaFormatExtensions
{
    /// <summary>
    /// The glob applied within the schema directory when none is configured explicitly.
    /// </summary>
    public static string DefaultGlob(this SchemaFormat format) => format switch
    {
        SchemaFormat.Yaml => "**/*.yaml",
        SchemaFormat.Json => "**/*.json",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown schema format."),
    };
}
