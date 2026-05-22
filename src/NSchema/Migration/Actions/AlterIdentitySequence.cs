using NSchema.Schema;

namespace NSchema.Migration.Actions;

/// <summary>
/// Represents altering the identity sequence options of an existing column in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table with the column whose identity sequence options are to be altered.</param>
/// <param name="TableName">The name of the table containing the column whose identity sequence options are to be altered.</param>
/// <param name="ColumnName">The name of the column whose identity sequence options are to be altered.</param>
/// <param name="OldOptions">The current identity sequence options of the column before alteration. This may be null if the column did not previously have identity options.</param>
/// <param name="NewOptions">The new identity sequence options of the column after alteration. This may be null if the column is being altered to remove identity options.</param>
public sealed record AlterIdentitySequence(
    string SchemaName,
    string TableName,
    string ColumnName,
    IdentityOptions? OldOptions,
    IdentityOptions? NewOptions
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
