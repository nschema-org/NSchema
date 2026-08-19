using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents adding a new foreign key constraint to an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="ForeignKey">The definition of the foreign key constraint to be added.</param>
public sealed record AddForeignKey(
    ObjectAddress Table,
    ForeignKey ForeignKey
) : MigrationAction;
