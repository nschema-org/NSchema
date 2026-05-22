namespace NSchema.Migration.Plan;

/// <summary>
/// Represents a migration plan that outlines the necessary steps to migrate a database schema from its current state to a desired target state.
/// </summary>
/// <param name="Actions">The ordered list of migration actions that need to be executed to apply the changes described in the migration plan.</param>
public sealed record MigrationPlan(IReadOnlyList<MigrationAction> Actions)
{
    /// <summary>
    /// Indicates whether the migration plan contains any actions to execute.
    /// </summary>
    public bool IsEmpty => Actions.Count == 0;
}
