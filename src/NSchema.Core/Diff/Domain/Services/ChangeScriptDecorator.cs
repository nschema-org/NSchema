using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Attaches each change-event script to the diff node it accompanies.
/// </summary>
internal static class ChangeScriptDecorator
{
    public static Result<DatabaseDiff> Decorate(DatabaseDiff diff, IReadOnlyList<ChangeScript> scripts)
    {
        if (scripts.Count == 0)
        {
            return diff;
        }

        var targeted = new List<(ObjectAddress Table, ChangeScript Script)>();
        foreach (var script in scripts)
        {
            if (script.Target.TableAddress is { } table)
            {
                targeted.Add((table, script));
            }
        }

        var byTable = targeted
            .GroupBy(entry => entry.Table, entry => entry.Script)
            .ToDictionary(group => group.Key, IReadOnlyDictionary<ChangeTarget, ChangeScript> (group) => group
                .GroupBy(script => script.Target)
                .ToDictionary(target => target.Key, target => target.First()));

        diff = diff with
        {
            Schemas = [.. diff.Schemas.Select(schema => schema with
            {
                Tables = [.. schema.Tables.Select(table =>
                    table.Kind == ChangeKind.Modify && byTable.TryGetValue(new ObjectAddress(schema.Name, table.Name), out var tableScripts)
                        ? Attach(schema.Name, table, tableScripts)
                        : table)],
            })],
        };

        var attached = diff.ChangeScripts().ToHashSet();
        var diagnostics = scripts.Where(s => !attached.Contains(s)).Select(DiffDiagnostics.DeadMigration);

        return Result.From(diff, diagnostics);
    }

    /// <summary>
    /// Attaches each script to the member diff it accompanies, matching by trigger and member name. The script
    /// rides the node directly, so the linearizer runs it without a lookup.
    /// </summary>
    private static TableDiff Attach(SqlIdentifier schema, TableDiff table, IReadOnlyDictionary<ChangeTarget, ChangeScript> scripts)
    {
        return table with
        {
            Columns = table.Columns.Select(column => column switch
            {
                { Kind: ChangeKind.Add } when Match(ChangeTrigger.AddColumn, column.Name) is { } m => column with { MigrationScript = m },
                { Kind: ChangeKind.Modify, Type: not null } when Match(ChangeTrigger.AlterColumnType, column.Name) is { } m => column with { MigrationScript = m },
                _ => column,
            }).ToList(),
            PrimaryKeys = AttachConstraints(table.PrimaryKeys, Match, (pk, m) => pk with { MigrationScript = m }),
            UniqueConstraints = AttachConstraints(table.UniqueConstraints, Match, (uc, m) => uc with { MigrationScript = m }),
            ForeignKeys = AttachConstraints(table.ForeignKeys, Match, (fk, m) => fk with { MigrationScript = m }),
            Checks = AttachConstraints(table.Checks, Match, (check, m) => check with { MigrationScript = m }),
            ExclusionConstraints = AttachConstraints(table.ExclusionConstraints, Match, (ex, m) => ex with { MigrationScript = m }),
        };

        ChangeScript? Match(ChangeTrigger trigger, SqlIdentifier member) =>
            scripts.GetValueOrDefault(new ChangeTarget(schema, table.Name, member, trigger));
    }

    /// <summary>
    /// Attaches the matching add-constraint script to each added constraint.
    /// </summary>
    private static List<T> AttachConstraints<T>(
        IReadOnlyList<T> constraints,
        Func<ChangeTrigger, SqlIdentifier, ChangeScript?> match,
        Func<T, ChangeScript, T> attach)
        where T : IMigratableDiff =>
        [.. constraints.Select(c => c.Kind == ChangeKind.Add && match(ChangeTrigger.AddConstraint, c.Name) is { } m ? attach(c, m) : c)];
}
