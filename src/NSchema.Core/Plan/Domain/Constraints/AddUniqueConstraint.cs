using NSchema.Model;
using NSchema.Model.Constraints;

namespace NSchema.Plan.Domain.Constraints;

/// <summary>
/// Represents adding a new unique constraint to an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="UniqueConstraint">The definition of the unique constraint to be added.</param>
public sealed record AddUniqueConstraint(
    ObjectAddress Table,
    UniqueConstraint UniqueConstraint
) : MigrationAction;
