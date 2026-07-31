using NSchema.Diff.Domain;
using NSchema.Model;

namespace NSchema.Plan.Domain;

/// <summary>
/// The complete executable plan.
/// </summary>
/// <param name="Diff">The complete diff: the schema changes and the scripts that need to be run.</param>
/// <param name="Statements">The ordered SQL statements that will actually be executed.</param>
/// <param name="Managed">The managed identities an apply of this plan establishes.</param>
/// <param name="Adopted">The existing identities an apply of this plan takes over.</param>
public sealed record MigrationPlan(
    DatabaseDiff Diff,
    IReadOnlyList<SqlStatement> Statements,
    IdentitySet? Managed = null,
    IdentitySet? Adopted = null
)
{
    /// <summary>
    /// The managed identities an apply of this plan establishes.
    /// </summary>
    public IdentitySet Managed { get; init; } = Managed ?? IdentitySet.Empty;

    /// <summary>
    /// The existing identities applying this plan will take over.
    /// </summary>
    public IdentitySet Adopted { get; init; } = Adopted ?? IdentitySet.Empty;

    /// <summary>
    /// Gets a value indicating whether an apply of this plan would do nothing at all.
    /// </summary>
    public bool IsEmpty => Diff.IsEmpty && !HasStatements && Adopted.IsEmpty;

    /// <summary>
    /// Gets a value indicating whether the plan carries SQL for an apply to execute.
    /// </summary>
    public bool HasStatements => Statements.Count > 0;
}
