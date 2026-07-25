using NSchema.Model;
using NSchema.Model.Constraints;

namespace NSchema.Plan.Domain.Constraints;

/// <summary>
/// Represents adding a new exclusion constraint to an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="ExclusionConstraint">The definition of the exclusion constraint to be added.</param>
public sealed record AddExclusionConstraint(
    ObjectAddress Table,
    ExclusionConstraint ExclusionConstraint
) : MigrationAction;
