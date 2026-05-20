namespace NSchema.Instructions;

public sealed record AlterColumnNullability(
    string SchemaName,
    string TableName,
    string ColumnName,
    bool WasNullable,
    bool IsNullable
) : SchemaInstruction
{
    public override bool IsDestructive => true;
}