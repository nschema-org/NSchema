using NSchema.Model;
using NSchema.Model.Indexes;

namespace NSchema.Plan.Domain.Indexes;

/// <summary>
/// Represents adding a new index to an existing relation in the database schema.
/// </summary>
/// <param name="Table">The address of the relation the index attaches to.</param>
/// <param name="Index">The definition of the index to be added.</param>
/// <param name="OnView">Whether the relation is a view rather than a table.</param>
public sealed record CreateIndex(
    ObjectAddress Table,
    TableIndex Index,
    bool OnView = false
) : MigrationAction;
