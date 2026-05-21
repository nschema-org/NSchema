using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Actions;

public sealed record CreateIndex(
    string SchemaName,
    string TableName,
    TableIndex Index
) : SchemaAction
{
    public override bool IsDestructive => false;
}
