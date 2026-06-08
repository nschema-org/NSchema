using System.Text.Json.Serialization;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the database provider that supplies the current (live) schema.
/// </summary>
internal sealed class ProviderConfig
{
    /// <summary>
    /// PostgreSQL provider settings.
    /// </summary>
    public PostgresProviderConfig? Postgres { get; set; }

    /// <summary>
    /// The number of provider sections populated. Zero means offline (no live schema source).
    /// </summary>
    [JsonIgnore]
    public int ConfiguredSectionCount => Postgres is not null ? 1 : 0;

    /// <summary>
    /// Returns the PostgreSQL section, creating it on first use.
    /// </summary>
    public PostgresProviderConfig EnsurePostgres() => Postgres ??= new PostgresProviderConfig();
}
