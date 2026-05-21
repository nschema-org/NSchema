using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Actions;

public sealed record AddForeignKey(
    string SchemaName,
    string TableName,
    ForeignKey ForeignKey
) : SchemaAction
{
    public override bool IsDestructive => false;
}
