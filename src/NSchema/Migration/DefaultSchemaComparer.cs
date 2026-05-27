using Microsoft.Extensions.Logging;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

internal sealed partial class DefaultSchemaComparer(ILogger<DefaultSchemaComparer> logger) : ISchemaComparer
{
    public MigrationPlan Compare(DatabaseSchema current, DatabaseSchema desired)
    {
        LogBeginningComparison();

        var actions = new List<MigrationAction>();
        CompareSchemas(current.Schemas, desired.Schemas, actions);

        LogComparisonComplete(actions.Count);

        return new MigrationPlan(actions, desired);
    }

    private void CompareSchemas(IReadOnlyList<SchemaDefinition> current, IReadOnlyList<SchemaDefinition> desired, List<MigrationAction> actions)
    {
        foreach (var currentSchema in current)
        {
            if (desired.Any(d => d.Name == currentSchema.Name || d.OldName == currentSchema.Name))
            {
                LogSchemaExists(currentSchema.Name);
            }
            else
            {
                LogSchemaNotInDesired(currentSchema.Name);
                actions.Add(new DropSchema(currentSchema.Name));
            }
        }

        foreach (var desiredSchema in desired)
        {
            var matchingCurrent = current.FirstOrDefault(schema => schema.Name == desiredSchema.Name || schema.Name == desiredSchema.OldName);
            if (matchingCurrent is null)
            {
                LogSchemaNew(desiredSchema.Name);
                actions.Add(new CreateSchema(desiredSchema.Name));
                foreach (var table in desiredSchema.Tables)
                {
                    AddNewTable(desiredSchema.Name, table, actions);
                }
                if (desiredSchema.Comment is not null)
                {
                    actions.Add(new SetSchemaComment(desiredSchema.Name, null, desiredSchema.Comment));
                }
                foreach (var grant in desiredSchema.Grants)
                {
                    actions.Add(new GrantSchemaUsage(desiredSchema.Name, grant.Role));
                }
            }
            else
            {
                if (matchingCurrent.Name == desiredSchema.Name)
                {
                    LogSchemaUnchanged(desiredSchema.Name);
                }
                else
                {
                    LogSchemaRenamed(matchingCurrent.Name, desiredSchema.Name);
                    actions.Add(new RenameSchema(matchingCurrent.Name, desiredSchema.Name));
                }

                if (matchingCurrent.Comment != desiredSchema.Comment)
                {
                    LogSchemaCommentChanged(desiredSchema.Name);
                    actions.Add(new SetSchemaComment(desiredSchema.Name, matchingCurrent.Comment, desiredSchema.Comment));
                }

                CompareSchemaGrants(desiredSchema.Name, matchingCurrent.Grants, desiredSchema.Grants, actions);
                CompareTables(desiredSchema.Name, matchingCurrent.Tables, desiredSchema, actions);
            }
        }
    }

    private void CompareTables(string schemaName, IReadOnlyList<Table> current, SchemaDefinition desired, List<MigrationAction> actions)
    {
        var droppedTables = desired.DroppedTables;

        foreach (var currentTable in current)
        {
            if (desired.Tables.Any(d => d.Name == currentTable.Name || d.OldName == currentTable.Name))
            {
                LogTableExists(schemaName, currentTable.Name);
            }
            else if (droppedTables.Contains(currentTable.Name, StringComparer.OrdinalIgnoreCase))
            {
                LogTableExplicitlyDropped(schemaName, currentTable.Name);
                actions.Add(new DropTable(schemaName, currentTable.Name));
            }
            else if (!desired.IsPartial)
            {
                LogTableNotInDesired(schemaName, currentTable.Name);
                actions.Add(new DropTable(schemaName, currentTable.Name));
            }
            else
            {
                LogTableSkippedPartial(schemaName, currentTable.Name);
            }
        }

        foreach (var desiredTable in desired.Tables)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredTable.Name || c.Name == desiredTable.OldName);
            if (matchingCurrent is null)
            {
                LogTableNew(schemaName, desiredTable.Name);
                AddNewTable(schemaName, desiredTable, actions);
            }
            else
            {
                if (matchingCurrent.Name == desiredTable.Name)
                {
                    LogTableUnchanged(schemaName, desiredTable.Name);
                }
                else
                {
                    LogTableRenamed(schemaName, matchingCurrent.Name, desiredTable.Name);
                    actions.Add(new RenameTable(schemaName, matchingCurrent.Name, desiredTable.Name));
                }

                if (matchingCurrent.Comment != desiredTable.Comment)
                {
                    LogTableCommentChanged(schemaName, desiredTable.Name);
                    actions.Add(new SetTableComment(schemaName, desiredTable.Name, matchingCurrent.Comment, desiredTable.Comment));
                }

                CompareColumns(schemaName, desiredTable.Name, matchingCurrent.Columns, desiredTable.Columns, actions);
                ComparePrimaryKey(schemaName, desiredTable.Name, matchingCurrent.PrimaryKey, desiredTable.PrimaryKey, actions);
                CompareForeignKeys(schemaName, desiredTable.Name, matchingCurrent.ForeignKeys, desiredTable.ForeignKeys, actions);
                CompareIndexes(schemaName, desiredTable.Name, matchingCurrent.Indexes, desiredTable.Indexes, actions);
                CompareTableGrants(schemaName, desiredTable.Name, matchingCurrent.Grants, desiredTable.Grants, actions);
            }
        }
    }

    private void CompareColumns(string schemaName, string tableName, IReadOnlyList<Column> current, IReadOnlyList<Column> desired, List<MigrationAction> actions)
    {
        foreach (var currentCol in current)
        {
            if (desired.Any(d => d.Name == currentCol.Name || d.OldName == currentCol.Name))
            {
                LogColumnExists(schemaName, tableName, currentCol.Name);
            }
            else
            {
                LogColumnNotInDesired(schemaName, tableName, currentCol.Name);
                actions.Add(new DropColumn(schemaName, tableName, currentCol.Name));
            }
        }

        foreach (var desiredCol in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredCol.Name || c.Name == desiredCol.OldName);
            if (matchingCurrent is null)
            {
                LogColumnNew(schemaName, tableName, desiredCol.Name);
                actions.Add(new AddColumn(schemaName, tableName, desiredCol));
                if (desiredCol.Comment is not null)
                {
                    actions.Add(new SetColumnComment(schemaName, tableName, desiredCol.Name, null, desiredCol.Comment));
                }

                continue;
            }

            if (matchingCurrent.Name == desiredCol.Name)
            {
                LogColumnUnchanged(schemaName, tableName, desiredCol.Name);
            }
            else
            {
                LogColumnRenamed(schemaName, tableName, matchingCurrent.Name, desiredCol.Name);
                actions.Add(new RenameColumn(schemaName, tableName, matchingCurrent.Name, desiredCol.Name));
            }

            if (matchingCurrent.Type == desiredCol.Type)
            {
                LogColumnTypeUnchanged(schemaName, tableName, desiredCol.Name, desiredCol.Type);
            }
            else
            {
                LogColumnTypeChanged(schemaName, tableName, desiredCol.Name, matchingCurrent.Type, desiredCol.Type);
                actions.Add(new AlterColumnType(schemaName, tableName, desiredCol.Name, matchingCurrent.Type, desiredCol.Type));
            }

            if (matchingCurrent.IsNullable == desiredCol.IsNullable)
            {
                LogColumnNullabilityUnchanged(schemaName, tableName, desiredCol.Name, desiredCol.IsNullable ? "NULL" : "NOT NULL");
            }
            else
            {
                LogColumnNullabilityChanged(schemaName, tableName, desiredCol.Name, matchingCurrent.IsNullable, desiredCol.IsNullable);
                actions.Add(new AlterColumnNullability(schemaName, tableName, desiredCol.Name, matchingCurrent.IsNullable, desiredCol.IsNullable));
            }

            if (matchingCurrent.DefaultExpression == desiredCol.DefaultExpression)
            {
                LogColumnDefaultUnchanged(schemaName, tableName, desiredCol.Name, desiredCol.DefaultExpression ?? "no default");
            }
            else
            {
                LogColumnDefaultChanged(schemaName, tableName, desiredCol.Name, matchingCurrent.DefaultExpression, desiredCol.DefaultExpression);
                actions.Add(new SetColumnDefault(schemaName, tableName, desiredCol.Name,
                    matchingCurrent.DefaultExpression, desiredCol.DefaultExpression));
            }

            if (matchingCurrent.Comment != desiredCol.Comment)
            {
                LogColumnCommentChanged(schemaName, tableName, desiredCol.Name);
                actions.Add(new SetColumnComment(schemaName, tableName, desiredCol.Name, matchingCurrent.Comment, desiredCol.Comment));
            }

            if (matchingCurrent.IsIdentity && desiredCol.IsIdentity
                && matchingCurrent.IdentityOptions != desiredCol.IdentityOptions)
            {
                LogColumnIdentityChanged(schemaName, tableName, desiredCol.Name,
                    matchingCurrent.IdentityOptions?.StartWith, desiredCol.IdentityOptions?.StartWith,
                    matchingCurrent.IdentityOptions?.MinValue, desiredCol.IdentityOptions?.MinValue,
                    matchingCurrent.IdentityOptions?.IncrementBy, desiredCol.IdentityOptions?.IncrementBy);
                actions.Add(new AlterIdentitySequence(schemaName, tableName, desiredCol.Name, matchingCurrent.IdentityOptions, desiredCol.IdentityOptions));
            }
        }
    }

    private void ComparePrimaryKey(string schemaName, string tableName, PrimaryKey? current, PrimaryKey? desired, List<MigrationAction> actions)
    {
        if (current?.Equals(desired) ?? desired == null)
        {
            LogPrimaryKeyUnchanged(schemaName, tableName);
            return;
        }

        if (current is not null)
        {
            LogPrimaryKeyDropping(current.Name, schemaName, tableName);
            actions.Add(new DropPrimaryKey(schemaName, tableName, current.Name));
        }

        if (desired is not null)
        {
            LogPrimaryKeyAdding(desired.Name, schemaName, tableName);
            actions.Add(new AddPrimaryKey(schemaName, tableName, desired));
        }
    }

    private void CompareForeignKeys(string schemaName, string tableName, IReadOnlyList<ForeignKey> current, IReadOnlyList<ForeignKey> desired, List<MigrationAction> actions)
    {
        foreach (var currentFk in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentFk.Name);
            if (matchingDesired is null || !currentFk.Equals(matchingDesired))
            {
                LogForeignKeyMissingOrChanged(currentFk.Name, schemaName, tableName);
                actions.Add(new DropForeignKey(schemaName, tableName, currentFk.Name));
            }
            else
            {
                LogForeignKeyUnchanged(currentFk.Name, schemaName, tableName);
            }
        }

        foreach (var desiredFk in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredFk.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredFk))
            {
                LogForeignKeyNewOrChanged(desiredFk.Name, schemaName, tableName);
                actions.Add(new AddForeignKey(schemaName, tableName, desiredFk));
            }
            else
            {
                LogForeignKeyUnchanged(desiredFk.Name, schemaName, tableName);
            }
        }
    }

    private void CompareIndexes(string schemaName, string tableName, IReadOnlyList<TableIndex> current, IReadOnlyList<TableIndex> desired, List<MigrationAction> actions)
    {
        foreach (var currentIdx in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentIdx.Name);
            if (matchingDesired is null || !currentIdx.Equals(matchingDesired))
            {
                LogIndexMissingOrChanged(currentIdx.Name, schemaName, tableName);
                actions.Add(new DropIndex(schemaName, tableName, currentIdx.Name));
            }
            else
            {
                if (currentIdx.Comment != matchingDesired.Comment)
                {
                    LogIndexCommentChanged(currentIdx.Name, schemaName, tableName);
                    actions.Add(new SetIndexComment(schemaName, tableName, currentIdx.Name, currentIdx.Comment, matchingDesired.Comment));
                }
                else
                {
                    LogIndexUnchanged(currentIdx.Name, schemaName, tableName);
                }
            }
        }

        foreach (var desiredIdx in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredIdx.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredIdx))
            {
                LogIndexNewOrChanged(desiredIdx.Name, schemaName, tableName);
                actions.Add(new CreateIndex(schemaName, tableName, desiredIdx));
                if (desiredIdx.Comment is not null)
                {
                    actions.Add(new SetIndexComment(schemaName, tableName, desiredIdx.Name, null, desiredIdx.Comment));
                }
            }
            else
            {
                LogIndexUnchanged(desiredIdx.Name, schemaName, tableName);
            }
        }
    }

    private void CompareSchemaGrants(string schemaName, IReadOnlyList<SchemaGrant> current, IReadOnlyList<SchemaGrant> desired, List<MigrationAction> actions)
    {
        foreach (var g in current.Where(c => desired.All(d => d.Role != c.Role)))
        {
            LogSchemaUsageRevoking(schemaName, g.Role);
            actions.Add(new RevokeSchemaUsage(schemaName, g.Role));
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            LogSchemaUsageGranting(schemaName, g.Role);
            actions.Add(new GrantSchemaUsage(schemaName, g.Role));
        }
    }

    private void CompareTableGrants(string schemaName, string tableName, IReadOnlyList<TableGrant> current, IReadOnlyList<TableGrant> desired, List<MigrationAction> actions)
    {
        foreach (var g in current)
        {
            var matching = desired.FirstOrDefault(d => d.Role == g.Role);
            if (matching is null)
            {
                LogTablePrivilegesRevoking(schemaName, tableName, g.Role);
                actions.Add(new RevokeTablePrivileges(schemaName, tableName, g.Role, g.Privileges));
            }
            else if (matching.Privileges != g.Privileges)
            {
                LogTablePrivilegesUpdating(schemaName, tableName, g.Role);
                actions.Add(new RevokeTablePrivileges(schemaName, tableName, g.Role, g.Privileges));
                actions.Add(new GrantTablePrivileges(schemaName, tableName, g.Role, matching.Privileges));
            }
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            LogTablePrivilegesGranting(schemaName, tableName, g.Role);
            actions.Add(new GrantTablePrivileges(schemaName, tableName, g.Role, g.Privileges));
        }
    }

    private void AddNewTable(string schemaName, Table table, List<MigrationAction> actions)
    {
        LogTableCreating(schemaName, table.Name);
        actions.Add(new CreateTable(schemaName, table));

        foreach (var fk in table.ForeignKeys)
        {
            LogForeignKeyAddingToNewTable(fk.Name, schemaName, table.Name);
            actions.Add(new AddForeignKey(schemaName, table.Name, fk));
        }

        foreach (var idx in table.Indexes)
        {
            LogIndexAddingToNewTable(idx.Name, schemaName, table.Name);
            actions.Add(new CreateIndex(schemaName, table.Name, idx));
            if (idx.Comment is not null)
            {
                actions.Add(new SetIndexComment(schemaName, table.Name, idx.Name, null, idx.Comment));
            }
        }

        if (table.Comment is not null)
        {
            actions.Add(new SetTableComment(schemaName, table.Name, null, table.Comment));
        }

        foreach (var col in table.Columns.Where(c => c.Comment is not null))
        {
            actions.Add(new SetColumnComment(schemaName, table.Name, col.Name, null, col.Comment));
        }

        foreach (var grant in table.Grants)
        {
            LogTablePrivilegesGrantingToNewTable(schemaName, table.Name, grant.Role);
            actions.Add(new GrantTablePrivileges(schemaName, table.Name, grant.Role, grant.Privileges));
        }
    }
}
