namespace NSchema.Cli.Configuration.Schema;

internal static class SchemaFormatExtensions
{
    /// <summary>
    /// The glob applied within the schema directory when none is configured explicitly.
    /// </summary>
    public static string DefaultPattern(this SchemaFormat format) => format switch
    {
        SchemaFormat.Yaml => "**/*.yaml",
        SchemaFormat.Json => "**/*.json",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown schema format."),
    };

    /// <summary>
    /// The file extension (without a leading dot) for schema files of this format.
    /// </summary>
    public static string Extension(this SchemaFormat format) => format switch
    {
        SchemaFormat.Yaml => "yaml",
        SchemaFormat.Json => "json",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown schema format."),
    };
}