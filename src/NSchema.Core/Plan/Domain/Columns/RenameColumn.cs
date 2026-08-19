using NSchema.Model;

namespace NSchema.Plan.Domain.Columns;

/// <summary>
/// Represents the renaming of an existing column in a table in the database schema.
/// </summary>
/// <param name="Column">The address of the column.</param>
/// <param name="NewName">The new name for the column to be renamed.</param>
public sealed record RenameColumn(
    MemberAddress Column,
    SqlIdentifier NewName
) : MigrationAction;
