using NSchema.Diff.Domain.Columns;
using NSchema.Diff.Domain.Constraints;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;

namespace NSchema.Diff.Domain.Services;

internal sealed partial class DatabaseComparer
{
    private List<TableDiff> CompareTables(SqlIdentifier schemaName, IReadOnlyList<Table> current, Schema desired, RenameLog renames)
    {
        var result = new List<TableDiff>();
        var (forDesired, forCurrent) = NamedEntityMatcher.Match(current, desired.Tables);

        for (var j = 0; j < current.Count; j++)
        {
            var currentTable = current[j];
            if (forCurrent[j] is not null)
            {
                LogTableExists(schemaName, currentTable.Name);
            }
            else
            {
                LogTableNotInDesired(schemaName, currentTable.Name);
                result.Add(TableDiff.Removed(schemaName, currentTable.Name));
            }
        }

        for (var i = 0; i < desired.Tables.Count; i++)
        {
            var desiredTable = desired.Tables[i];
            if (forDesired[i] is not { } matchingCurrent)
            {
                LogTableNew(schemaName, desiredTable.Name);
                result.Add(BuildNewTable(schemaName, desiredTable));
            }
            else
            {
                var renamedFrom = renames.RenamedFrom(ObjectAddress.Table(schemaName, desiredTable.Name));
                if (BuildModifiedTable(schemaName, matchingCurrent, desiredTable, renamedFrom, renames) is { } diff)
                {
                    result.Add(diff);
                }
            }
        }

        return result.OrderBy(t => t.Name).ToList();
    }

    private TableDiff BuildNewTable(SqlIdentifier schemaName, Table table)
    {
        LogTableCreating(schemaName, table.Name);
        var owner = new ObjectAddress(schemaName, table.Name);

        var columns = table.Columns.Select(ColumnDiff.Added).ToList();

        // Every list member arrives by diffing against an empty current side — the same path a modified table
        // takes, so a member's comment folds in as a trailing Modify either way.
        var foreignKeys = CompareForeignKeys(owner, [], table.ForeignKeys);
        var uniqueConstraints = CompareUniqueConstraints(owner, [], table.UniqueConstraints);
        var checks = CompareChecks(owner, [], table.CheckConstraints);
        var exclusions = CompareExclusionConstraints(owner, [], table.ExclusionConstraints);
        var indexes = CompareIndexes(owner, [], table.Indexes);
        var triggers = CompareTriggers(owner, [], table.Triggers);

        // The primary key is created inline by CREATE TABLE, but its comment still needs a separate set.
        var primaryKey = new List<PrimaryKeyDiff>();
        if (table.PrimaryKey is { Comment: not null } pk)
        {
            primaryKey.Add(PrimaryKeyDiff.CommentChanged(pk.Name, new ValueChange<string>(null, pk.Comment)));
        }

        var grants = new List<GrantChange>();
        foreach (var grant in table.Grants)
        {
            LogTablePrivilegesGrantingToNewTable(owner, grant.Role);
            grants.Add(new GrantChange(ChangeKind.Add, grant.Role, grant.Privileges));
        }

        var comment = table.Comment is not null ? new ValueChange<string>(null, table.Comment) : null;

        // The full table definition rides along so the linearizer can emit a single CREATE TABLE (with the
        // primary key and columns inline) without reconstructing it from the column diffs. The primary key is
        // therefore created inline (no PrimaryKeyDiff); everything else arrives as a separate add.
        return TableDiff.Added(schemaName, table) with
        {
            Comment = comment,
            Columns = columns,
            Grants = grants,
            Indexes = indexes,
            PrimaryKeys = primaryKey,
            ForeignKeys = foreignKeys,
            UniqueConstraints = uniqueConstraints,
            Checks = checks,
            ExclusionConstraints = exclusions,
            Triggers = triggers,
        };
    }

    private TableDiff? BuildModifiedTable(SqlIdentifier schemaName, Table current, Table desired, SqlIdentifier? renamedFrom, RenameLog renames)
    {
        if (renamedFrom is null)
        {
            LogTableUnchanged(schemaName, desired.Name);
        }
        else
        {
            LogTableRenamed(schemaName, renamedFrom, desired.Name);
        }

        ValueChange<string>? comment = null;
        if (current.Comment != desired.Comment)
        {
            LogTableCommentChanged(schemaName, desired.Name);
            comment = new ValueChange<string>(current.Comment, desired.Comment);
        }

        var owner = new ObjectAddress(schemaName, desired.Name);
        var columns = CompareColumns(owner, current.Columns, desired.Columns, renames);

        var primaryKey = ComparePrimaryKey(owner, current.PrimaryKey, desired.PrimaryKey);
        var foreignKeys = CompareForeignKeys(owner, current.ForeignKeys, desired.ForeignKeys);
        var uniqueConstraints = CompareUniqueConstraints(owner, current.UniqueConstraints, desired.UniqueConstraints);
        var checks = CompareChecks(owner, current.CheckConstraints, desired.CheckConstraints);
        var exclusions = CompareExclusionConstraints(owner, current.ExclusionConstraints, desired.ExclusionConstraints);

        var indexes = CompareIndexes(owner, current.Indexes, desired.Indexes);
        var grants = CompareTableGrants(owner, current.Grants, desired.Grants);
        var triggers = CompareTriggers(owner, current.Triggers, desired.Triggers);

        var hasChange = renamedFrom is not null || comment is not null || columns.Count > 0
            || primaryKey.Count > 0 || foreignKeys.Count > 0 || uniqueConstraints.Count > 0 || checks.Count > 0
            || exclusions.Count > 0 || indexes.Count > 0 || grants.Count > 0 || triggers.Count > 0;
        if (!hasChange)
        {
            return null;
        }

        return TableDiff.Modified(schemaName, desired.Name) with
        {
            RenamedFrom = renamedFrom,
            Comment = comment,
            Columns = columns,
            Grants = grants,
            Indexes = indexes,
            PrimaryKeys = primaryKey,
            ForeignKeys = foreignKeys,
            UniqueConstraints = uniqueConstraints,
            Checks = checks,
            ExclusionConstraints = exclusions,
            Triggers = triggers,
        };
    }
}
