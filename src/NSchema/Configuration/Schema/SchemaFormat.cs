namespace NSchema.Configuration.Schema;

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
