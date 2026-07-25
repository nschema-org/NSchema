using NSchema.Model;
using NSchema.Model.Indexes;

namespace NSchema.Plan.Domain.Indexes;

/// <summary>
/// Represents adding a new index to an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="Index">The definition of the index to be added.</param>
public sealed record CreateIndex(
    ObjectAddress Table,
    TableIndex Index
) : MigrationAction;
