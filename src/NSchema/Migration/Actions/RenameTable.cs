namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the renaming of an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to be renamed.</param>
/// <param name="OldName">The current name of the table to be renamed.</param>
/// <param name="NewName">The new name for the table to be renamed.</param>
public sealed record RenameTable(string SchemaName, string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
