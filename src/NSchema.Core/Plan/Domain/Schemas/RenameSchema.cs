using NSchema.Model;

namespace NSchema.Plan.Domain.Schemas;

/// <summary>
/// Represents the renaming of an existing schema in the database schema.
/// </summary>
/// <param name="OldName">The current name of the schema to be renamed.</param>
/// <param name="NewName">The new name for the schema to be renamed.</param>
public sealed record RenameSchema(SqlIdentifier OldName, SqlIdentifier NewName) : MigrationAction;
