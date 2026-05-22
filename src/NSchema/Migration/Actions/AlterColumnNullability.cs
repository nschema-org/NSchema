namespace NSchema.Migration.Actions;

/// <summary>
/// Represents altering the nullability of an existing column in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table with the column to be altered.</param>
/// <param name="TableName">The name of the table containing the column to be altered.</param>
/// <param name="ColumnName">The name of the column whose nullability is to be altered.</param>
/// <param name="OldNullable">A boolean value indicating the current nullability of the column.</param>
/// <param name="NewNullable">A boolean value indicating the new nullability of the column after alteration.</param>
/// <remarks>
/// This action can either make a column nullable or non-nullable, depending on the specified parameters.
/// Altering a column's nullability may lead to data loss if changing from nullable to non-nullable, as existing null values would need to be handled appropriately.
/// </remarks>
public sealed record AlterColumnNullability(
    string SchemaName,
    string TableName,
    string ColumnName,
    bool OldNullable,
    bool NewNullable
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
