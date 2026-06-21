using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;

namespace NSchema.Configuration.Provider;

/// <summary>
/// Configures the database provider that supplies the current (live) schema.
/// </summary>
internal sealed class ProviderConfig : IBindable
{
    private static readonly OptionBinding<string> ConnectionStringBinding = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString)
        .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

    private static readonly OptionBinding<string> UsernameBinding = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresUsername)
        .FromProjectConfig(c => c.Provider?.Postgres?.Username);

    private static readonly OptionBinding<string> PasswordBinding = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresPassword)
        .FromProjectConfig(c => c.Provider?.Postgres?.Password);

    private static readonly OptionBinding<int?> CommandTimeoutBinding = OptionBinding.Create<int?>()
        .FromProjectConfig(c => c.Provider?.Postgres?.CommandTimeout);

    private static readonly OptionBinding<string> SqliteConnectionStringBinding = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.SqliteConnectionString)
        .FromProjectConfig(c => c.Provider?.Sqlite?.ConnectionString);

    /// <summary>
    /// PostgreSQL provider settings.
    /// </summary>
    public PostgresProviderConfig? Postgres { get; set; }

    /// <summary>
    /// SQLite provider settings.
    /// </summary>
    public SqliteProviderConfig? Sqlite { get; set; }

    /// <summary>
    /// The number of provider sections populated. Zero means offline (no live schema source).
    /// </summary>
    public int ConfiguredSectionCount => (Postgres is not null ? 1 : 0) + (Sqlite is not null ? 1 : 0);

    /// <summary>
    /// Maps a <c>PROVIDER</c> block onto a typed config, selecting the section from the block's label.
    /// </summary>
    public static ProviderConfig FromBlock(ConfigBlock block) =>
        block.Label?.ToLowerInvariant() switch
        {
            "postgres" => new ProviderConfig { Postgres = PostgresProviderConfig.FromBlock(block) },
            "sqlite" => new ProviderConfig { Sqlite = SqliteProviderConfig.FromBlock(block) },
            _ => throw new InvalidOperationException($"Unknown or missing provider '{block.Label}' in a PROVIDER block. Expected 'postgres' or 'sqlite'."),
        };

    /// <summary>
    /// Resolves the provider's connection settings from the project config, environment, and command line.
    /// </summary>
    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        ConnectionStringBinding.Bind(project, cli, cs => EnsurePostgres().ConnectionString = cs);
        UsernameBinding.Bind(project, cli, u => EnsurePostgres().Username = u);
        PasswordBinding.Bind(project, cli, p => EnsurePostgres().Password = p);
        CommandTimeoutBinding.Bind(project, cli, t => EnsurePostgres().CommandTimeout = t);
        SqliteConnectionStringBinding.Bind(project, cli, cs => EnsureSqlite().ConnectionString = cs);
    }

    private PostgresProviderConfig EnsurePostgres() => Postgres ??= new PostgresProviderConfig();

    private SqliteProviderConfig EnsureSqlite() => Sqlite ??= new SqliteProviderConfig();
}
