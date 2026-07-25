using NSchema.Diff.Domain;
using NSchema.Model;

namespace NSchema.Plan.Domain;

/// <summary>
/// The complete executable plan.
/// </summary>
/// <param name="Diff">The complete diff: the schema changes and the scripts that need to be run.</param>
/// <param name="Statements">The ordered SQL statements that will actually be executed.</param>
/// <param name="Managed">The managed identities an apply of this plan establishes.</param>
public sealed record MigrationPlan(DatabaseDiff Diff, IReadOnlyList<SqlStatement> Statements, IdentitySet? Managed = null)
{
    /// <summary>
    /// The managed identities an apply of this plan establishes.
    /// </summary>
    public IdentitySet Managed { get; init; } = Managed ?? IdentitySet.Empty;

    /// <summary>
    /// Gets a value indicating whether the plan contains no statements to execute.
    /// </summary>
    public bool IsEmpty => Statements.Count == 0;
}
