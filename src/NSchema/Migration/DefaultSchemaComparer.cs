using Microsoft.Extensions.Logging;
using NSchema.Migration.Actions;
using NSchema.Schema;

namespace NSchema.Migration;

public sealed class DefaultSchemaComparer(ILogger<DefaultSchemaComparer> logger) : ISchemaComparer
{
    public SchemaPlan Compare(DatabaseSchema current, DatabaseSchema desired)
    {
        logger.LogDebug("Beginning schema comparison");

        var actions = new List<SchemaAction>();
        CompareSchemas(current.Schemas, desired.Schemas, actions);

        logger.LogDebug("Comparison complete: {ActionCount} actions generated", actions.Count);

        return new SchemaPlan(actions);
    }

    private void CompareSchemas(IReadOnlyList<SchemaDefinition> current, IReadOnlyList<SchemaDefinition> desired, List<SchemaAction> actions)
    {
        foreach (var currentSchema in current)
        {
            if (desired.Any(d => d.Name == currentSchema.Name || d.PreviousName == currentSchema.Name))
            {
                logger.LogDebug("Schema '{Schema}' exists in desired state", currentSchema.Name);
            }
            else
            {
                logger.LogInformation("Schema '{Schema}' not found in desired state", currentSchema.Name);
                actions.Add(new DropSchema(currentSchema.Name));
            }
        }

        foreach (var desiredSchema in desired)
        {
            var matchingCurrent = current.FirstOrDefault(schema => schema.Name == desiredSchema.Name || schema.Name == desiredSchema.PreviousName);
            if (matchingCurrent is null)
            {
                logger.LogInformation("Schema '{Schema}' is new", desiredSchema.Name);
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
                    logger.LogDebug("Schema '{Schema}' is unchanged", desiredSchema.Name);
                }
                else
                {
                    logger.LogInformation("Schema '{OldName}' renamed to '{NewName}'", matchingCurrent.Name, desiredSchema.Name);
                    actions.Add(new RenameSchema(matchingCurrent.Name, desiredSchema.Name));
                }

                if (matchingCurrent.Comment != desiredSchema.Comment)
                {
                    logger.LogInformation("Schema '{Schema}' comment changed", desiredSchema.Name);
                    actions.Add(new SetSchemaComment(desiredSchema.Name, matchingCurrent.Comment, desiredSchema.Comment));
                }

                CompareSchemaGrants(desiredSchema.Name, matchingCurrent.Grants, desiredSchema.Grants, actions);
                CompareTables(desiredSchema.Name, matchingCurrent.Tables, desiredSchema, actions);
            }
        }
    }

    private void CompareTables(string schemaName, IReadOnlyList<Table> current, SchemaDefinition desired, List<SchemaAction> actions)
    {
        var droppedTables = desired.DroppedTables;

        foreach (var currentTable in current)
        {
            if (desired.Tables.Any(d => d.Name == currentTable.Name || d.PreviousName == currentTable.Name))
            {
                logger.LogDebug("Table '{Schema}.{Table}' exists in desired state", schemaName, currentTable.Name);
            }
            else if (droppedTables.Contains(currentTable.Name, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogInformation("Table '{Schema}.{Table}' explicitly marked for removal", schemaName, currentTable.Name);
                actions.Add(new DropTable(schemaName, currentTable.Name));
            }
            else if (!desired.IsPartial)
            {
                logger.LogInformation("Table '{Schema}.{Table}' not found in desired state", schemaName, currentTable.Name);
                actions.Add(new DropTable(schemaName, currentTable.Name));
            }
            else
            {
                logger.LogInformation("Table '{Schema}.{Table}' not in desired state; skipping (partial schema)", schemaName, currentTable.Name);
            }
        }

        foreach (var desiredTable in desired.Tables)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredTable.Name || c.Name == desiredTable.PreviousName);
            if (matchingCurrent is null)
            {
                logger.LogInformation("Table '{Schema}.{Table}' is new", schemaName, desiredTable.Name);
                AddNewTable(schemaName, desiredTable, actions);
            }
            else
            {
                if (matchingCurrent.Name == desiredTable.Name)
                {
                    logger.LogDebug("Table '{Schema}.{Table}' is unchanged", schemaName, desiredTable.Name);
                }
                else
                {
                    logger.LogInformation("Table '{Schema}.{OldName}' renamed to '{NewName}'", schemaName, matchingCurrent.Name, desiredTable.Name);
                    actions.Add(new RenameTable(schemaName, matchingCurrent.Name, desiredTable.Name));
                }

                if (matchingCurrent.Comment != desiredTable.Comment)
                {
                    logger.LogInformation("Table '{Schema}.{Table}' comment changed", schemaName, desiredTable.Name);
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

    private void CompareColumns(string schemaName, string tableName, IReadOnlyList<Column> current, IReadOnlyList<Column> desired, List<SchemaAction> actions)
    {
        foreach (var currentCol in current)
        {
            if (desired.Any(d => d.Name == currentCol.Name || d.PreviousName == currentCol.Name))
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' exists in desired state", schemaName, tableName, currentCol.Name);
            }
            else
            {
                logger.LogInformation("Column '{Schema}.{Table}.{Column}' not found in desired state", schemaName, tableName, currentCol.Name);
                actions.Add(new DropColumn(schemaName, tableName, currentCol.Name));
            }
        }

        foreach (var desiredCol in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredCol.Name || c.Name == desiredCol.PreviousName);
            if (matchingCurrent is null)
            {
                logger.LogInformation("Column '{Schema}.{Table}.{Column}' is new", schemaName, tableName, desiredCol.Name);
                actions.Add(new AddColumn(schemaName, tableName, desiredCol));
                if (desiredCol.Comment is not null)
                    actions.Add(new SetColumnComment(schemaName, tableName, desiredCol.Name, null, desiredCol.Comment));
                continue;
            }

            if (matchingCurrent.Name == desiredCol.Name)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' is unchanged", schemaName, tableName, desiredCol.Name);
            }
            else
            {
                logger.LogInformation("Column '{Schema}.{Table}.{OldName}' renamed to '{NewName}'", schemaName, tableName, matchingCurrent.Name, desiredCol.Name);
                actions.Add(new RenameColumn(schemaName, tableName, matchingCurrent.Name, desiredCol.Name));
            }

            if (matchingCurrent.Type == desiredCol.Type)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' type is unchanged ({Type})", schemaName, tableName, desiredCol.Name, desiredCol.Type);
            }
            else
            {
                logger.LogInformation("Column '{Schema}.{Table}.{Column}' type changed: {OldType} -> {NewType}", schemaName, tableName, desiredCol.Name, matchingCurrent.Type, desiredCol.Type);
                actions.Add(new AlterColumnType(schemaName, tableName, desiredCol.Name, matchingCurrent.Type, desiredCol.Type));
            }

            if (matchingCurrent.IsNullable == desiredCol.IsNullable)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' nullability is unchanged ({Nullability})",
                    schemaName, tableName, desiredCol.Name, desiredCol.IsNullable ? "NULL" : "NOT NULL");
            }
            else
            {
                logger.LogInformation("Column '{Schema}.{Table}.{Column}' nullability changed: {OldValue} -> {NewValue}", schemaName, tableName, desiredCol.Name, matchingCurrent.IsNullable, desiredCol.IsNullable);
                actions.Add(new AlterColumnNullability(schemaName, tableName, desiredCol.Name, matchingCurrent.IsNullable, desiredCol.IsNullable));
            }

            if (matchingCurrent.DefaultExpression == desiredCol.DefaultExpression)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' default is unchanged ({Default})", schemaName, tableName, desiredCol.Name, desiredCol.DefaultExpression ?? "no default");
            }
            else
            {
                logger.LogInformation("Column '{Schema}.{Table}.{Column}' default changed: '{OldDefault}' -> '{NewDefault}'",
                    schemaName, tableName, desiredCol.Name, matchingCurrent.DefaultExpression,
                    desiredCol.DefaultExpression);
                actions.Add(new SetColumnDefault(schemaName, tableName, desiredCol.Name,
                    matchingCurrent.DefaultExpression, desiredCol.DefaultExpression));
            }

            if (matchingCurrent.Comment != desiredCol.Comment)
            {
                logger.LogInformation("Column '{Schema}.{Table}.{Column}' comment changed", schemaName, tableName, desiredCol.Name);
                actions.Add(new SetColumnComment(schemaName, tableName, desiredCol.Name, matchingCurrent.Comment, desiredCol.Comment));
            }

            if (matchingCurrent.IsIdentity && desiredCol.IsIdentity
                && matchingCurrent.IdentityOptions != desiredCol.IdentityOptions)
            {
                logger.LogInformation(
                    "Column '{Schema}.{Table}.{Column}' identity sequence options changed: start {OldStart} -> {NewStart}, min {OldMin} -> {NewMin}, increment {OldIncrement} -> {NewIncrement}",
                    schemaName, tableName, desiredCol.Name,
                    matchingCurrent.IdentityOptions?.StartWith, desiredCol.IdentityOptions?.StartWith,
                    matchingCurrent.IdentityOptions?.MinValue, desiredCol.IdentityOptions?.MinValue,
                    matchingCurrent.IdentityOptions?.IncrementBy, desiredCol.IdentityOptions?.IncrementBy);
                actions.Add(new AlterIdentitySequence(schemaName, tableName, desiredCol.Name, matchingCurrent.IdentityOptions, desiredCol.IdentityOptions));
            }
        }
    }

    private void ComparePrimaryKey(string schemaName, string tableName, PrimaryKey? current, PrimaryKey? desired, List<SchemaAction> actions)
    {
        if (current?.Equals(desired) ?? desired == null)
        {
            logger.LogDebug("Primary key for '{Schema}.{Table}' is unchanged", schemaName, tableName);
            return;
        }

        if (current is not null)
        {
            logger.LogInformation("Dropping primary key '{KeyName}' from '{Schema}.{Table}'", current.Name, schemaName,
                tableName);
            actions.Add(new DropPrimaryKey(schemaName, tableName, current.Name));
        }

        if (desired is not null)
        {
            logger.LogInformation("Adding primary key '{KeyName}' to '{Schema}.{Table}'", desired.Name, schemaName,
                tableName);
            actions.Add(new AddPrimaryKey(schemaName, tableName, desired));
        }
    }

    private void CompareForeignKeys(string schemaName, string tableName, IReadOnlyList<ForeignKey> current, IReadOnlyList<ForeignKey> desired, List<SchemaAction> actions)
    {
        foreach (var currentFk in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentFk.Name);
            if (matchingDesired is null || !currentFk.Equals(matchingDesired))
            {
                logger.LogInformation("Foreign key '{FkName}' on '{Schema}.{Table}' is missing or changed", currentFk.Name,
                    schemaName, tableName);
                actions.Add(new DropForeignKey(schemaName, tableName, currentFk.Name));
            }
            else
            {
                logger.LogDebug("Foreign key '{FkName}' on '{Schema}.{Table}' is unchanged", currentFk.Name, schemaName,
                    tableName);
            }
        }

        foreach (var desiredFk in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredFk.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredFk))
            {
                logger.LogInformation("Foreign key '{FkName}' on '{Schema}.{Table}' is new or changed", desiredFk.Name,
                    schemaName, tableName);
                actions.Add(new AddForeignKey(schemaName, tableName, desiredFk));
            }
            else
            {
                logger.LogDebug("Foreign key '{FkName}' on '{Schema}.{Table}' is unchanged", desiredFk.Name, schemaName,
                    tableName);
            }
        }
    }

    private void CompareIndexes(string schemaName, string tableName, IReadOnlyList<TableIndex> current, IReadOnlyList<TableIndex> desired, List<SchemaAction> actions)
    {
        foreach (var currentIdx in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentIdx.Name);
            if (matchingDesired is null || !currentIdx.Equals(matchingDesired))
            {
                logger.LogInformation("Index '{IndexName}' on '{Schema}.{Table}' is missing or changed", currentIdx.Name,
                    schemaName, tableName);
                actions.Add(new DropIndex(schemaName, tableName, currentIdx.Name));
            }
            else
            {
                if (currentIdx.Comment != matchingDesired.Comment)
                {
                    logger.LogInformation("Index '{IndexName}' on '{Schema}.{Table}' comment changed", currentIdx.Name, schemaName, tableName);
                    actions.Add(new SetIndexComment(schemaName, tableName, currentIdx.Name, currentIdx.Comment, matchingDesired.Comment));
                }
                else
                {
                    logger.LogDebug("Index '{IndexName}' on '{Schema}.{Table}' is unchanged", currentIdx.Name, schemaName, tableName);
                }
            }
        }

        foreach (var desiredIdx in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredIdx.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredIdx))
            {
                logger.LogInformation("Index '{IndexName}' on '{Schema}.{Table}' is new or changed", desiredIdx.Name,
                    schemaName, tableName);
                actions.Add(new CreateIndex(schemaName, tableName, desiredIdx));
                if (desiredIdx.Comment is not null)
                    actions.Add(new SetIndexComment(schemaName, tableName, desiredIdx.Name, null, desiredIdx.Comment));
            }
            else
            {
                logger.LogDebug("Index '{IndexName}' on '{Schema}.{Table}' is unchanged", desiredIdx.Name, schemaName,
                    tableName);
            }
        }
    }

    private void CompareSchemaGrants(string schemaName, IReadOnlyList<SchemaGrant> current, IReadOnlyList<SchemaGrant> desired, List<SchemaAction> actions)
    {
        foreach (var g in current.Where(c => desired.All(d => d.Role != c.Role)))
        {
            logger.LogInformation("Revoking USAGE on schema '{Schema}' from '{Role}'", schemaName, g.Role);
            actions.Add(new RevokeSchemaUsage(schemaName, g.Role));
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            logger.LogInformation("Granting USAGE on schema '{Schema}' to '{Role}'", schemaName, g.Role);
            actions.Add(new GrantSchemaUsage(schemaName, g.Role));
        }
    }

    private void CompareTableGrants(string schemaName, string tableName, IReadOnlyList<TableGrant> current, IReadOnlyList<TableGrant> desired, List<SchemaAction> actions)
    {
        foreach (var g in current)
        {
            var matching = desired.FirstOrDefault(d => d.Role == g.Role);
            if (matching is null)
            {
                logger.LogInformation("Revoking all privileges on '{Schema}.{Table}' from '{Role}'", schemaName, tableName, g.Role);
                actions.Add(new RevokeTablePrivileges(schemaName, tableName, g.Role, g.Privileges));
            }
            else if (matching.Privileges != g.Privileges)
            {
                logger.LogInformation("Updating privileges on '{Schema}.{Table}' for '{Role}'", schemaName, tableName, g.Role);
                actions.Add(new RevokeTablePrivileges(schemaName, tableName, g.Role, g.Privileges));
                actions.Add(new GrantTablePrivileges(schemaName, tableName, g.Role, matching.Privileges));
            }
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            logger.LogInformation("Granting privileges on '{Schema}.{Table}' to '{Role}'", schemaName, tableName, g.Role);
            actions.Add(new GrantTablePrivileges(schemaName, tableName, g.Role, g.Privileges));
        }
    }

    private void AddNewTable(string schemaName, Table table, List<SchemaAction> actions)
    {
        logger.LogInformation("Creating table '{Schema}.{Table}'", schemaName, table.Name);
        actions.Add(new CreateTable(schemaName, table));

        foreach (var fk in table.ForeignKeys)
        {
            logger.LogInformation("Adding foreign key '{FkName}' to new table '{Schema}.{Table}'", fk.Name, schemaName, table.Name);
            actions.Add(new AddForeignKey(schemaName, table.Name, fk));
        }

        foreach (var idx in table.Indexes)
        {
            logger.LogInformation("Adding index '{IndexName}' to new table '{Schema}.{Table}'", idx.Name, schemaName, table.Name);
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
            logger.LogInformation("Granting privileges on new table '{Schema}.{Table}' to '{Role}'", schemaName, table.Name, grant.Role);
            actions.Add(new GrantTablePrivileges(schemaName, table.Name, grant.Role, grant.Privileges));
        }
    }
}
