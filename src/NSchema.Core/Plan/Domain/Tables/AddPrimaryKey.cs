using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents adding a new primary key constraint to an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="PrimaryKey">The definition of the primary key constraint to be added.</param>
public sealed record AddPrimaryKey(
    ObjectAddress Table,
    PrimaryKey PrimaryKey
) : MigrationAction;
