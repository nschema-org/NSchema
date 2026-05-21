using System.Collections.Frozen;
using NSchema.Migration.Actions;

namespace NSchema.Migration;

public sealed class ActionOrderingTransformer : IMigrationPlanTransformer
{
    private static readonly IReadOnlyDictionary<Type, int> s_priorities = new List<Type> {
        typeof(RunPreDeploymentScript),
        typeof(DropForeignKey),
        typeof(DropIndex),
        typeof(DropPrimaryKey),
        typeof(RenameSchema),
        typeof(CreateSchema),
        typeof(RenameTable),
        typeof(CreateTable),
        typeof(DropColumn),
        typeof(RenameColumn),
        typeof(AddColumn),
        typeof(AlterColumnType),
        typeof(AlterColumnNullability),
        typeof(SetColumnDefault),
        typeof(AddPrimaryKey),
        typeof(AddForeignKey),
        typeof(CreateIndex),
        typeof(DropTable),
        typeof(DropSchema),
        typeof(RunPostDeploymentScript),
    }.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

    public MigrationPlan Transform(MigrationPlan plan)
    {
        var actions = plan.Actions.OrderBy(a => s_priorities[a.GetType()]).ToList();
        return new MigrationPlan(actions);
    }
}
