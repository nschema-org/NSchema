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

    public void SetProvider(ProviderType type)
    {
        switch (type)
        {
            case ProviderType.Postgres: Postgres ??= new PostgresProviderConfig(); break;
            default: throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported provider type: {type}");
        }
    }

    public void SetConnectionString(string connectionString)
    {
        if (Postgres is null)
        {
            throw new InvalidOperationException("Cannot set connection string when no provider type is configured; specify a provider type first.");
        }
        Postgres.ConnectionString = connectionString;
    }
}
