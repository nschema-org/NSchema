namespace NSchema.Domain.Migration;

public sealed record DropColumn(string SchemaName, string TableName, string ColumnName) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
