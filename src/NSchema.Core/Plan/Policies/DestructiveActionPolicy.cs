using NSchema.Diff.Domain;
using NSchema.Diff.Domain.CompositeTypes;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Domains;
using NSchema.Diff.Domain.Enums;
using NSchema.Diff.Domain.Routines;
using NSchema.Diff.Domain.Sequences;
using NSchema.Diff.Domain.Tables;
using NSchema.Diff.Domain.Views;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Columns;
using NSchema.Plan.Domain.CompositeTypes;
using NSchema.Plan.Domain.Constraints;
using NSchema.Plan.Domain.Domains;
using NSchema.Plan.Domain.Enums;
using NSchema.Plan.Domain.Extensions;
using NSchema.Plan.Domain.Routines;
using NSchema.Plan.Domain.Schemas;
using NSchema.Plan.Domain.Sequences;
using NSchema.Plan.Domain.Tables;
using NSchema.Plan.Domain.Views;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports the destructive changes in a plan.
/// </summary>
internal sealed class DestructiveActionPolicy : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        var destructive = DestructiveChanges(plan.Diff).Distinct().ToList();

        return destructive.Count == 0
            ? []
            : [DestructiveActionDiagnostics.DestructiveChange(string.Join(", ", destructive))];
    }

    private static IEnumerable<string> DestructiveChanges(DatabaseDiff diff)
    {
        // Dropping a database-global extension removes shared infrastructure (and anything that depended on it),
        // so it is destructive.
        foreach (var extension in diff.Extensions.Where(e => e.Change == ChangeKind.Remove))
        {
            yield return nameof(DropExtension);
        }

        foreach (var schema in diff.Schemas)
        {
            if (schema.Change == ChangeKind.Remove)
            {
                yield return nameof(DropSchema);
            }

            foreach (var grant in schema.Grants.Where(g => g.Change == ChangeKind.Remove))
            {
                yield return nameof(RevokeSchemaUsage);
            }

            // A removal of any named object is destructive: its definition (and, for tables, its data) is lost
            // from managed state. Only whole-object removals count — a routine's signature-change recreate is a
            // declared edit (the database blocks the underlying drop loudly if dependents exist), and dropping
            // a check constraint only loosens validation, so neither is flagged here.
            foreach (var obj in schema.EnumerateObjects().Where(o => o.Change == ChangeKind.Remove))
            {
                yield return obj switch
                {
                    TableDiff => nameof(DropTable),
                    ViewDiff => nameof(DropView),
                    EnumDiff => nameof(DropEnum),
                    SequenceDiff => nameof(DropSequence),
                    RoutineDiff => nameof(DropRoutine),
                    DomainDiff => nameof(DropDomain),
                    CompositeTypeDiff => nameof(DropCompositeType),
                    _ => throw new ArgumentOutOfRangeException(nameof(diff), $"Unhandled object diff type: {obj.GetType().Name}"),
                };
            }

            // Dropping a field from a composite type removes that attribute from every row of every table whose
            // column uses the type, so it is destructive — the analogue of dropping a column.
            foreach (var type in schema.CompositeTypes.Where(t => t.Change != ChangeKind.Remove))
            {
                foreach (var field in type.Fields.Where(f => f.Change == ChangeKind.Remove))
                {
                    yield return nameof(DropCompositeField);
                }
            }

            foreach (var table in schema.Tables)
            {
                foreach (var grant in table.Grants.Where(g => g.Change == ChangeKind.Remove))
                {
                    yield return nameof(RevokeTablePrivileges);
                }

                // A column may also be destructively *modified* (narrowing its type or tightening nullability),
                // not just removed.
                foreach (var column in table.Columns)
                {
                    if (column.Change == ChangeKind.Remove)
                    {
                        yield return nameof(DropColumn);
                    }

                    if (column.Type is not null || column.Nullability is not null)
                    {
                        yield return nameof(AlterColumn);
                    }
                }

                // Dropping a key or unique constraint removes a structural guarantee (and a unique constraint
                // may be a foreign-key target), so those are destructive; dropping a check only loosens
                // validation and an index can be rebuilt, so neither is flagged.
                foreach (var member in table.EnumerateMembers().Where(m => m.Change == ChangeKind.Remove))
                {
                    var actionName = member switch
                    {
                        PrimaryKeyDiff => nameof(DropPrimaryKey),
                        ForeignKeyDiff => nameof(DropForeignKey),
                        UniqueConstraintDiff => nameof(DropUniqueConstraint),
                        ExclusionConstraintDiff => nameof(DropExclusionConstraint),
                        _ => null, // columns are flagged above; checks and indexes are not destructive
                    };

                    if (actionName is not null)
                    {
                        yield return actionName;
                    }
                }
            }
        }
    }
}
