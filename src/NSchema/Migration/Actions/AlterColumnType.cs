using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record AlterColumnType(
    string SchemaName,
    string TableName,
    string ColumnName,
    SqlType OldType,
    SqlType NewType
) : MigrationAction
{
    public override bool IsDestructive => true;
}
