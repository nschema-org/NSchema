namespace NSchema.Migration.Actions;

/// <summary>
/// Represents the modification of the default value of an existing column in a table within the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table in which the column's default value will be modified.</param>
/// <param name="TableName">The name of the table in which the column's default value will be modified.</param>
/// <param name="ColumnName">The name of the column whose default value will be modified.</param>
/// <param name="OldDefault">The current default value of the column before modification. This can be null if there is no existing default value.</param>
/// <param name="NewDefault">The new default value to be set on the column after modification. This can be null if the default value is being removed.</param>
public sealed record SetColumnDefault(
    string SchemaName,
    string TableName,
    string ColumnName,
    string? OldDefault,
    string? NewDefault
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
