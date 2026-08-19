using NSchema.Model;

namespace NSchema.Plan.Domain.Constraints;

/// <summary>
/// Represents the removal of an existing check constraint from a table in the database schema. Dropping a check
/// only loosens validation (no data is lost), so it is not treated as a destructive action.
/// </summary>
/// <param name="Constraint">The address of the constraint.</param>
public sealed record DropCheckConstraint(
    MemberAddress Constraint
) : MigrationAction;
