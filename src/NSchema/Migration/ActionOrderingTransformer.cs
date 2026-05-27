using System.Collections.Frozen;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// A migration plan transformer that orders migration actions based on a predefined priority list.
/// </summary>
internal sealed class ActionOrderingTransformer : IMigrationPlanTransformer
{
    // A little dramatic, but it works.
    private const int PreScriptPriority = int.MinValue;
    private const int PostScriptPriority = int.MaxValue;

    public static readonly IReadOnlyDictionary<Type, int> Priorities = new List<Type> {
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

        // These types have special rules. They are kept here to satisfy the integration test
        // and show that these actions are known to the ordering algorithm.
        typeof(RunScript),
    }.Index().ToFrozenDictionary(x => x.Item, x => x.Index);

    public MigrationPlan Transform(MigrationPlan plan)
    {
        var actions = plan.Actions.OrderBy(GetPriority).ToList();
        return plan with { Actions = actions };
    }

    private static int GetPriority(MigrationAction action) => action switch
    {
        RunScript { Script.Type: ScriptType.PreDeployment } => PreScriptPriority,
        RunScript { Script.Type: ScriptType.PostDeployment } => PostScriptPriority,
        _ => Priorities[action.GetType()],
    };
}
