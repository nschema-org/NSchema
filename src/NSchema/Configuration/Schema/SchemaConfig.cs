using System.Text.Json.Serialization;

namespace NSchema.Configuration.Schema;

/// <summary>
/// Configures how the desired schema is located and read. Required for the plan and apply commands.
/// </summary>
internal sealed class SchemaConfig
{
    /// <summary>
    /// The directory the desired-schema files are discovered under.
    /// </summary>
    [JsonPropertyName("dir")]
    public string Directory { get; set; } = "";

    /// <summary>
    /// The glob matched within <see cref="Directory"/>. When null, the default <c>**/*.sql</c> glob is used.
    /// </summary>
    public string? Pattern { get; set; }
}
