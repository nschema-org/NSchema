using System.Collections.Frozen;
using NSchema.Model;
using NSchema.Model.Services;
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
using NSchema.Plan.Domain.Services;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Triggers;
using NSchema.Plan.Domain.Views;

namespace NSchema.Plan.Domain;

/// <summary>
/// Orders migration actions by their database dependencies.
/// </summary>
internal static class MigrationActionOrdering
{
    private static readonly IReadOnlyDictionary<Type, int> _priorities = new Type[][]
    {
        [typeof(RenameSchema)], [typeof(DropView)], [typeof(DropTrigger)], [typeof(DropForeignKey)],
        [typeof(DropCheckConstraint)], [typeof(DropExclusionConstraint)], [typeof(DropUniqueConstraint)],
        [typeof(DropIndex)], [typeof(DropPrimaryKey)], [typeof(RevokeSchemaUsage)], [typeof(RevokeTablePrivileges)],
        [typeof(CreateExtension)], [typeof(AlterExtension)], [typeof(CreateSchema)], [typeof(RenameEnum)],
        [typeof(RenameSequence)], [typeof(CreateEnum)], [typeof(CreateSequence)], [typeof(AddEnumValue)],
        [typeof(AlterSequence)], [typeof(RenameDomain)], [typeof(CreateDomain)], [typeof(RecreateDomain)],
        [typeof(AlterDomainDefault)], [typeof(AlterDomainNotNull)], [typeof(AddDomainCheck)], [typeof(DropDomainCheck)],
        [typeof(RenameCompositeType)], [typeof(CreateCompositeType)], [typeof(AddCompositeField)],
        [typeof(AlterCompositeFieldType)], [typeof(DropCompositeField)],
        [typeof(RenameTable)], [typeof(RenameView)], [typeof(DropColumn)],
        [typeof(RenameColumn)], [typeof(AddColumn)], [typeof(RenameRoutine)],
        [typeof(CreateTable), typeof(CreateRoutine), typeof(ReplaceRoutine), typeof(RecreateRoutine)],
        [typeof(ExecuteScript)], [typeof(AlterColumn)],
        [typeof(AlterIdentitySequence)], [typeof(SetColumnDefault)], [typeof(SetColumnGenerated)],
        [typeof(AddPrimaryKey)],
        [typeof(AddUniqueConstraint)], [typeof(AddForeignKey)], [typeof(AddCheckConstraint)], [typeof(AddExclusionConstraint)],
        [typeof(CreateIndex)], [typeof(CreateTrigger)], [typeof(ReplaceTrigger)], [typeof(CreateView)], [typeof(ReplaceView)], [typeof(GrantSchemaUsage)],
        [typeof(GrantTablePrivileges)], [typeof(SetSchemaComment)], [typeof(SetTableComment)], [typeof(SetColumnComment)],
        [typeof(SetIndexComment)], [typeof(SetTriggerComment)], [typeof(SetConstraintComment)], [typeof(SetViewComment)],
        [typeof(SetEnumComment)], [typeof(SetSequenceComment)], [typeof(SetRoutineComment)], [typeof(SetDomainComment)],
        [typeof(SetCompositeTypeComment)], [typeof(SetExtensionComment)],
        [typeof(DropRoutine), typeof(DropTable)],
        [typeof(DropDomain)], [typeof(DropCompositeType)], [typeof(DropEnum)], [typeof(DropSequence)], [typeof(DropSchema)],
        [typeof(DropExtension)],
    }.Index()
        .SelectMany(band => band.Item.Select(type => (Type: type, band.Index)))
        .ToFrozenDictionary(item => item.Type, item => item.Index);

    public static IReadOnlyList<MigrationAction> Order(IReadOnlyList<MigrationAction> actions, PlanDependencies dependencies)
    {
        var creates = SubjectIndex(actions, CreateSubject);
        var drops = SubjectIndex(actions, DropSubject);

        var edges = new List<DependencyEdge>();
        foreach (var (address, index) in creates)
        {
            foreach (var (dependency, certainty) in dependencies.RequiresEdges(address))
            {
                if (creates.TryGetValue(dependency, out var dependencyIndex))
                {
                    edges.Add(new DependencyEdge(index, dependencyIndex, Strength(certainty)));
                }
            }
        }

        foreach (var (address, index) in drops)
        {
            foreach (var (dependent, certainty) in dependencies.RequiredByEdges(address))
            {
                if (drops.TryGetValue(dependent, out var dependentIndex))
                {
                    edges.Add(new DependencyEdge(index, dependentIndex, Strength(certainty)));
                }
            }
        }

        // Containment and anchoring: an action on an object follows the object's create, a member's removal
        // precedes its object's drop, and a change script stays after its anchor's create and before its drop.
        // These edges are structural facts, so they carry a stated strength.
        const int structural = 1;
        const int inferred = 0;
        for (var i = 0; i < actions.Count; i++)
        {
            switch (actions[i])
            {
                case ExecuteScript { Anchor: { } anchor }:
                    AfterCreate(i, anchor);
                    BeforeDrop(i, anchor);
                    break;

                case AddForeignKey foreignKey:
                    AfterCreate(i, foreignKey.Table);
                    AfterCreate(i, foreignKey.ForeignKey.References);
                    break;

                // A trigger's references ride its own action, not its table's create: the trigger runs later,
                // so folding them into the table would manufacture cycles that do not exist. Scanned guesses,
                // so they carry inferred strength and give way first in a genuine cycle.
                case CreateTrigger trigger:
                    AfterCreate(i, trigger.Table);
                    foreach (var reference in trigger.Trigger.References(trigger.Table.Schema))
                    {
                        AfterCreate(i, reference, inferred);
                    }
                    break;

                case ReplaceTrigger trigger:
                    AfterCreate(i, trigger.Table);
                    foreach (var reference in trigger.Trigger.References(trigger.Table.Schema))
                    {
                        AfterCreate(i, reference, inferred);
                    }
                    break;

                default:
                    if (AttendantSubject(actions[i]) is { } touched)
                    {
                        AfterCreate(i, touched);
                    }
                    if (MemberDropSubject(actions[i]) is { } shrinking)
                    {
                        BeforeDrop(i, shrinking);
                    }
                    break;
            }
        }

        return actions.OrderedByDependencies(a => PriorityOf(a), edges);

        void AfterCreate(int index, ObjectAddress subject, int strength = structural)
        {
            if (creates.TryGetValue(subject, out var create) && create != index)
            {
                edges.Add(new DependencyEdge(index, create, strength));
            }
        }

        void BeforeDrop(int index, ObjectAddress subject)
        {
            if (drops.TryGetValue(subject, out var drop) && drop != index)
            {
                edges.Add(new DependencyEdge(drop, index, structural));
            }
        }
    }

    /// <summary>The object an attendant action operates on — what must exist before it runs.</summary>
    private static ObjectAddress? AttendantSubject(MigrationAction action) => action switch
    {
        AddColumn x => x.Table,
        AlterColumn x => x.Table,
        AddPrimaryKey x => x.Table,
        AddUniqueConstraint x => x.Table,
        AddCheckConstraint x => x.Table,
        AddExclusionConstraint x => x.Table,
        CreateIndex x => x.Table,
        SetTableComment x => x.Table,
        GrantTablePrivileges x => x.Table,
        SetColumnDefault x => x.Column.Owner,
        SetColumnGenerated x => x.Column.Owner,
        SetColumnComment x => x.Column.Owner,
        AlterIdentitySequence x => x.Column.Owner,
        SetConstraintComment x => x.Constraint.Owner,
        SetIndexComment x => x.Index.Owner,
        SetTriggerComment x => x.Trigger.Owner,
        SetViewComment x => x.View,
        SetRoutineComment x => x.Routine,
        SetDomainComment x => x.Domain,
        AlterDomainDefault x => x.Domain,
        AlterDomainNotNull x => x.Domain,
        AddDomainCheck x => x.Domain,
        SetEnumComment x => x.Enum,
        AddEnumValue x => x.Enum,
        SetSequenceComment x => x.Sequence,
        AlterSequence x => x.Sequence,
        SetCompositeTypeComment x => x.Type,
        AddCompositeField x => x.Type,
        _ => null,
    };

    /// <summary>The object a member-removal shrinks — what cannot drop until this has run.</summary>
    private static ObjectAddress? MemberDropSubject(MigrationAction action) => action switch
    {
        DropColumn x => x.Table,
        DropTrigger x => x.Trigger.Owner,
        DropIndex x => x.Index.Owner,
        DropForeignKey x => x.ForeignKey.Owner,
        DropPrimaryKey x => x.PrimaryKey.Owner,
        DropUniqueConstraint x => x.Constraint.Owner,
        DropCheckConstraint x => x.Constraint.Owner,
        DropExclusionConstraint x => x.Constraint.Owner,
        RevokeTablePrivileges x => x.Table,
        DropDomainCheck x => x.Check.Owner,
        DropCompositeField x => x.Field.Owner,
        _ => null,
    };

    internal static bool HasPriority(Type actionType) => _priorities.ContainsKey(actionType);

    private static int PriorityOf(MigrationAction action) => _priorities.TryGetValue(action.GetType(), out var priority)
        ? priority
        : throw new InvalidOperationException($"Migration action '{action.GetType().Name}' has no ordering priority.");

    private static Dictionary<ObjectAddress, int> SubjectIndex(
        IReadOnlyList<MigrationAction> actions, Func<MigrationAction, ObjectAddress?> subject)
    {
        var index = new Dictionary<ObjectAddress, int>();
        for (var i = 0; i < actions.Count; i++)
        {
            if (subject(actions[i]) is { } address)
            {
                index.TryAdd(address, i);
            }
        }

        return index;
    }

    /// <summary>The object a create-family action brings into (or back into) being.</summary>
    /// <remarks>
    /// A rename is deliberately absent: its subject is the pre-rename name, whose identity an address-keyed
    /// edge would conflate with the new one, so renames stay governed by the bands alone.
    /// </remarks>
    private static ObjectAddress? CreateSubject(MigrationAction action) => action switch
    {
        CreateTable x => new ObjectAddress(x.SchemaName, x.Table.Name),
        CreateView x => new ObjectAddress(x.SchemaName, x.View.Name),
        ReplaceView x => new ObjectAddress(x.SchemaName, x.View.Name),
        CreateRoutine x => new ObjectAddress(x.SchemaName, x.Routine.Name),
        ReplaceRoutine x => new ObjectAddress(x.SchemaName, x.Routine.Name),
        RecreateRoutine x => new ObjectAddress(x.SchemaName, x.Routine.Name),
        CreateDomain x => new ObjectAddress(x.SchemaName, x.DomainType.Name),
        RecreateDomain x => new ObjectAddress(x.SchemaName, x.DomainType.Name),
        CreateEnum x => new ObjectAddress(x.SchemaName, x.Enum.Name),
        CreateSequence x => new ObjectAddress(x.SchemaName, x.Sequence.Name),
        CreateCompositeType x => new ObjectAddress(x.SchemaName, x.CompositeType.Name),
        _ => null,
    };

    /// <summary>The object a drop-family action takes away.</summary>
    private static ObjectAddress? DropSubject(MigrationAction action) => action switch
    {
        DropTable x => x.Table,
        DropView x => x.View,
        DropRoutine x => x.Routine,
        DropDomain x => x.Domain,
        DropEnum x => x.Enum,
        DropSequence x => x.Sequence,
        DropCompositeType x => x.Type,
        _ => null,
    };

    // A cycle is cut at its weakest edge: an inferred guess gives way before a stated fact.
    private static int Strength(DependencyCertainty certainty) => certainty == DependencyCertainty.Stated ? 1 : 0;
}
