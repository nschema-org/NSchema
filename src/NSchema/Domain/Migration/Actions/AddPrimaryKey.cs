using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Actions;

public sealed record AddPrimaryKey(
    string SchemaName,
    string TableName,
    PrimaryKey PrimaryKey
) : SchemaAction
{
    public override bool IsDestructive => false;
}
