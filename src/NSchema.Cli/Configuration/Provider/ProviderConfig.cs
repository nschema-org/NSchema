using System.CommandLine;
using System.Text.Json.Serialization;

namespace NSchema.Cli.Configuration.Provider;

/// <summary>
/// Configures the database provider that supplies the current (live) schema.
/// </summary>
internal sealed class ProviderConfig : IConfigurable
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

    public void Configure(ParseResult result)
    {
        if (result.TryGetOverride(ProviderOptions.Type, EnvironmentVariables.Provider, Enum.Parse<ProviderType>, out var provider))
        {
            switch (provider)
            {
                case ProviderType.Postgres:
                    Postgres ??= new PostgresProviderConfig();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(provider), $"Unsupported provider type: {provider}");
            }
        }

        if (ProviderOptions.ConnectionStringOption.TryGetOverride(result, out var connectionString))
        {
            Postgres ??= new PostgresProviderConfig();
            Postgres.ConnectionString = connectionString;
        }
    }
}
