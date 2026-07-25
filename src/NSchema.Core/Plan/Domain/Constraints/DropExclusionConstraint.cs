using NSchema.Model;

namespace NSchema.Plan.Domain.Constraints;

/// <summary>
/// Represents the removal of an existing exclusion constraint from a table in the database schema.
/// </summary>
/// <param name="Constraint">The address of the constraint.</param>
public sealed record DropExclusionConstraint(
    MemberAddress Constraint
) : MigrationAction;
