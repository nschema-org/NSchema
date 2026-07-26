namespace NSchema.Commands.New;

/// <summary>
/// The database provider a scaffolded project is configured for.
/// </summary>
internal enum DatabaseKind
{
    /// <summary>
    /// PostgreSQL.
    /// </summary>
    Postgres,

    /// <summary>
    /// SQLite.
    /// </summary>
    Sqlite,

    /// <summary>
    /// Microsoft SQL Server.
    /// </summary>
    SqlServer,
}
