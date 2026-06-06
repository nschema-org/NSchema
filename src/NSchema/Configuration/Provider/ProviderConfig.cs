using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Configuration.Binding;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the database provider that supplies the current (live) schema.
/// </summary>
internal sealed class ProviderConfig : IBindable
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

    public void Bind(ParseResult result)
    {
        ProviderOptions.Type.Bind(result, p =>
            {
                switch (p)
                {
                    case ProviderType.Postgres:
                        Postgres ??= new PostgresProviderConfig();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(p), $"Unsupported provider type: {p}");
                }
            });

        ProviderOptions.ConnectionString.Bind(result, cs => (Postgres ??= new PostgresProviderConfig()).ConnectionString = cs);
    }
}
