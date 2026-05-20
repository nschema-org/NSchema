using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record AlterColumnType(
    string SchemaName,
    string TableName,
    string ColumnName,
    SqlType OldType,
    SqlType NewType
) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
