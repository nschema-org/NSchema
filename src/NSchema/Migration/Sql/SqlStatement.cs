namespace NSchema.Migration.Sql;

/// <summary>
/// A single SQL statement in a <see cref="Sql"/>, together with execution metadata.
/// </summary>
/// <param name="Sql">The SQL text to execute.</param>
/// <param name="RunOutsideTransaction">
/// When true, the executor will run this statement outside of any surrounding migration transaction.
/// Use for statements that the database disallows inside a transaction (for example, Postgres's
/// <c>CREATE INDEX CONCURRENTLY</c>) or for deployment scripts that manage their own transactions.
/// </param>
public sealed record SqlStatement(string Sql, bool RunOutsideTransaction = false);
