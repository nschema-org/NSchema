namespace NSchema.Domain.Migration;

public sealed record DropTable(string SchemaName, string TableName) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
