namespace NSchema.Domain.Migration;

public sealed record RenameSchema(string OldName, string NewName) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
