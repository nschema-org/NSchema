namespace NSchema.Migration;

/// <summary>
/// Represents a plan for executing a series of SQL statements as part of a database migration.
/// </summary>
/// <param name="Statements">An ordered list of SQL statements that need to be executed to apply the changes described in a migration plan.</param>
public sealed record SqlPlan(IReadOnlyList<SqlStatement> Statements)
{
    /// <summary>
    /// Gets a value indicating whether the SQL plan contains no statements to execute.
    /// </summary>
    public bool IsEmpty => Statements.Count == 0;
}
