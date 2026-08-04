using System.Collections.Frozen;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.CompositeTypes;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Domains;
using NSchema.Plan.Domain.Enums;
using NSchema.Plan.Domain.Extensions;
using NSchema.Plan.Domain.Indexes;
using NSchema.Plan.Domain.Routines;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Scripts;
using NSchema.Plan.Domain.Sequences;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Triggers;
using NSchema.Plan.Domain.Views;

namespace NSchema.Plan.Domain;

/// <summary>
/// Orders migration actions by their database dependencies.
/// </summary>
internal static class MigrationActionOrdering
{
    private static readonly IReadOnlyDictionary<Type, int> _priorities = new[]
    {
        typeof(RenameSchema), typeof(DropView), typeof(DropTrigger), typeof(DropForeignKey),
        typeof(DropCheckConstraint), typeof(DropExclusionConstraint), typeof(DropUniqueConstraint),
        typeof(DropIndex), typeof(DropPrimaryKey), typeof(RevokeSchemaUsage), typeof(RevokeTablePrivileges),
        typeof(CreateExtension), typeof(AlterExtension), typeof(CreateSchema), typeof(RenameEnum),
        typeof(RenameSequence), typeof(CreateEnum), typeof(CreateSequence), typeof(AddEnumValue),
        typeof(AlterSequence), typeof(RenameDomain), typeof(CreateDomain), typeof(RecreateDomain),
        typeof(AlterDomainDefault), typeof(AlterDomainNotNull), typeof(AddDomainCheck), typeof(DropDomainCheck),
        typeof(RenameCompositeType), typeof(CreateCompositeType), typeof(AddCompositeField),
        typeof(AlterCompositeFieldType), typeof(DropCompositeField),
        typeof(RenameTable), typeof(RenameView), typeof(CreateTable), typeof(DropColumn),
        typeof(RenameColumn), typeof(AddColumn), typeof(ExecuteScript), typeof(AlterColumn),
        typeof(AlterIdentitySequence), typeof(SetColumnDefault), typeof(SetColumnGenerated),
        typeof(RenameRoutine), typeof(CreateRoutine), typeof(ReplaceRoutine), typeof(RecreateRoutine),
        typeof(AddPrimaryKey),
        typeof(AddUniqueConstraint), typeof(AddForeignKey), typeof(AddCheckConstraint), typeof(AddExclusionConstraint),
        typeof(CreateIndex), typeof(CreateTrigger), typeof(ReplaceTrigger), typeof(CreateView), typeof(ReplaceView), typeof(GrantSchemaUsage),
        typeof(GrantTablePrivileges), typeof(SetSchemaComment), typeof(SetTableComment), typeof(SetColumnComment),
        typeof(SetIndexComment), typeof(SetTriggerComment), typeof(SetConstraintComment), typeof(SetViewComment),
        typeof(SetEnumComment), typeof(SetSequenceComment), typeof(SetRoutineComment), typeof(SetDomainComment),
        typeof(SetCompositeTypeComment), typeof(SetExtensionComment), typeof(DropRoutine), typeof(DropTable),
        typeof(DropDomain), typeof(DropCompositeType), typeof(DropEnum), typeof(DropSequence), typeof(DropSchema),
        typeof(DropExtension),
    }.Index().ToFrozenDictionary(item => item.Item, item => item.Index);

    public static IReadOnlyList<MigrationAction> Order(IEnumerable<MigrationAction> actions) =>
        [.. actions.OrderBy(PriorityOf)];

    internal static bool HasPriority(Type actionType) => _priorities.ContainsKey(actionType);

    private static int PriorityOf(MigrationAction action) => _priorities.TryGetValue(action.GetType(), out var priority)
        ? priority
        : throw new InvalidOperationException($"Migration action '{action.GetType().Name}' has no ordering priority.");
}
