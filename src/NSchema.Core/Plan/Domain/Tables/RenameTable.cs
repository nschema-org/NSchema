using NSchema.Model;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents the renaming of an existing table in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="NewName">The new name for the table to be renamed.</param>
public sealed record RenameTable(ObjectAddress Table, SqlIdentifier NewName) : MigrationAction;
