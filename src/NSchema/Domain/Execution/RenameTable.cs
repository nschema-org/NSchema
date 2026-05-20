namespace NSchema.Domain.Execution;

public sealed record RenameTable(string SchemaName, string OldName, string NewName) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
