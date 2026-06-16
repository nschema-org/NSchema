using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Dsl;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the database provider that supplies the current (live) schema.
/// </summary>
internal sealed class ProviderConfig : IBindable
{
    private static readonly OptionBinding<string> ConnectionStringBinding = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString)
        .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

    private static readonly OptionBinding<int?> CommandTimeoutBinding = OptionBinding.Create<int?>()
        .FromProjectConfig(c => c.Provider?.Postgres?.CommandTimeout);

    /// <summary>
    /// PostgreSQL provider settings.
    /// </summary>
    public PostgresProviderConfig? Postgres { get; set; }

    /// <summary>
    /// The number of provider sections populated. Zero means offline (no live schema source).
    /// </summary>
    public int ConfiguredSectionCount => Postgres is not null ? 1 : 0;

    /// <summary>
    /// Resolves the provider's connection settings from the project config, environment, and command line.
    /// </summary>
    public void Bind(DslProjectConfig project, ParseResult cli)
    {
        ConnectionStringBinding.Bind(project, cli, cs => EnsurePostgres().ConnectionString = cs);
        CommandTimeoutBinding.Bind(project, cli, t => EnsurePostgres().CommandTimeout = t);
    }

    private PostgresProviderConfig EnsurePostgres() => Postgres ??= new PostgresProviderConfig();
}
