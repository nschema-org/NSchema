using NSchema.Model;

namespace NSchema.Plan.Domain;

/// <summary>
/// A single SQL statement in a <see cref="Sql"/>, together with execution metadata.
/// </summary>
/// <param name="Sql">The SQL text to execute.</param>
/// <param name="RunOutsideTransaction">
/// When true, the executor will run this statement outside of any surrounding migration transaction.
/// Use for statements that the database disallows inside a transaction (for example, Postgres's
/// <c>CREATE INDEX CONCURRENTLY</c>) or for deployment scripts that manage their own transactions.
/// </param>
/// <param name="Action">The name of the <see cref="MigrationAction"/> this statement performs.</param>
public sealed record SqlStatement(SqlText Sql, bool RunOutsideTransaction = false, string? Action = null);
