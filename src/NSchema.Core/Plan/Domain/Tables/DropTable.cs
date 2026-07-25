using NSchema.Model;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents the removal of an existing table from the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
public sealed record DropTable(ObjectAddress Table) : MigrationAction;
