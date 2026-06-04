namespace NSchema.Cli.Configuration.Provider;

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
    /// The provider selected by the populated section, or null when none is configured.
    /// </summary>
    public ProviderType? SelectedType => Postgres is not null ? ProviderType.Postgres : null;

    /// <summary>
    /// The number of populated provider sections. Used to enforce the "exactly one" rule.
    /// </summary>
    public int ConfiguredSectionCount => Postgres is null ? 0 : 1;
}
