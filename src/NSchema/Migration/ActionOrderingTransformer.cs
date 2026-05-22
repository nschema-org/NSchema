using System.Collections.Frozen;
using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// A migration plan transformer that orders migration actions based on a predefined priority list.
/// </summary>
internal sealed class ActionOrderingTransformer : IMigrationPlanTransformer
{
    public static readonly IReadOnlyDictionary<Type, int> Priorities = new List<Type> {
        typeof(RunPreDeploymentScript),
        typeof(DropForeignKey),
        typeof(DropIndex),
        typeof(DropPrimaryKey),
        typeof(RevokeSchemaUsage),
        typeof(RevokeTablePrivileges),
        typeof(RenameSchema),
        typeof(CreateSchema),
        typeof(RenameTable),
        typeof(CreateTable),
        typeof(DropColumn),
        typeof(RenameColumn),
        typeof(AddColumn),
        typeof(AlterColumnType),
        typeof(AlterColumnNullability),
        typeof(AlterIdentitySequence),
        typeof(SetColumnDefault),
        typeof(AddPrimaryKey),
        typeof(AddForeignKey),
        typeof(CreateIndex),
        typeof(GrantSchemaUsage),
        typeof(GrantTablePrivileges),
        typeof(SetSchemaComment),
        typeof(SetTableComment),
        typeof(SetColumnComment),
        typeof(SetIndexComment),
        typeof(DropTable),
        typeof(DropSchema),
        typeof(RunPostDeploymentScript),
    }.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

    public MigrationPlan Transform(MigrationPlan plan)
    {
        var actions = plan.Actions.OrderBy(a => Priorities[a.GetType()]).ToList();
        return new MigrationPlan(actions);
    }
}
