namespace NSchema.Migration.Plan;

/// <summary>
/// Represents the renaming of an existing schema in the database schema.
/// </summary>
/// <param name="OldName">The current name of the schema to be renamed.</param>
/// <param name="NewName">The new name for the schema to be renamed.</param>
public sealed record RenameSchema(string OldName, string NewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
