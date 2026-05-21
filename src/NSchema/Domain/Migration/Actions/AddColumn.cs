using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Actions;

public sealed record AddColumn(string SchemaName, string TableName, Column Column) : SchemaAction
{
    public override bool IsDestructive => false;
}
