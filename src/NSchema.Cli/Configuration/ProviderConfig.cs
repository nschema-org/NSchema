namespace NSchema.Cli.Configuration;

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

    // The provider sections that have been populated. Used to enforce the "exactly one" rule.
    private IEnumerable<object> ConfiguredSections => new object?[] { Postgres }.OfType<object>();

    /// <summary>
    /// Validates the configuration, yielding one message per problem found.
    /// An unconfigured provider (offline mode) is valid and yields nothing.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (ConfiguredSections.Count() > 1)
        {
            yield return "More than one database provider is configured; specify exactly one.";
        }

        if (Postgres is { } postgres)
        {
            foreach (var error in postgres.Validate())
            {
                yield return error;
            }
        }
    }
}
