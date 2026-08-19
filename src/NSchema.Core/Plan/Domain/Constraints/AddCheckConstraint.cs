using NSchema.Model;
using NSchema.Model.Constraints;

namespace NSchema.Plan.Domain.Constraints;

/// <summary>
/// Represents adding a new check constraint to an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="CheckConstraint">The definition of the check constraint to be added.</param>
public sealed record AddCheckConstraint(
    ObjectAddress Table,
    CheckConstraint CheckConstraint
) : MigrationAction;
