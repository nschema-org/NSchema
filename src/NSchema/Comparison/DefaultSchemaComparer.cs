using Microsoft.Extensions.Logging;
using NSchema.Domain.Migration;
using NSchema.Domain.Migration.Actions;
using NSchema.Domain.Schema;

namespace NSchema.Comparison;

public sealed class DefaultSchemaComparer(ILogger<DefaultSchemaComparer> logger) : ISchemaComparer
{
    public MigrationPlan Compare(DatabaseSchema source, DatabaseSchema target)
    {
        logger.LogDebug("Beginning schema comparison");

        var actions = new List<SchemaAction>();

        foreach (var script in target.PreDeploymentScripts ?? [])
        {
            logger.LogDebug("Pre-deployment script '{Script}'", script);
            actions.Add(new RunPreDeploymentScript(script));
        }

        CompareSchemas(source.Schemas, target.Schemas, actions);

        foreach (var script in target.PostDeploymentScripts ?? [])
        {
            logger.LogDebug("Post-deployment script '{Script}'", script);
            actions.Add(new RunPostDeploymentScript(script));
        }

        logger.LogDebug("Comparison complete: {ActionCount} actions generated", actions.Count);

        return new MigrationPlan(actions);
    }

    private void CompareSchemas(IReadOnlyList<Schema> current, IReadOnlyList<Schema> desired, List<SchemaAction> actions)
    {
        foreach (var currentSchema in current)
        {
            if (desired.Any(d => d.Name == currentSchema.Name || d.PreviousName == currentSchema.Name))
            {
                logger.LogDebug("Schema '{Schema}' exists in desired state", currentSchema.Name);
            }
            else
            {
                logger.LogDebug("Schema '{Schema}' not found in desired state", currentSchema.Name);
                actions.Add(new DropSchema(currentSchema.Name));
            }
        }

        foreach (var desiredSchema in desired)
        {
            var matchingCurrent = current.FirstOrDefault(schema => schema.Name == desiredSchema.Name || schema.Name == desiredSchema.PreviousName);
            if (matchingCurrent is null)
            {
                logger.LogDebug("Schema '{Schema}' is new", desiredSchema.Name);
                actions.Add(new CreateSchema(desiredSchema.Name));
                foreach (var table in desiredSchema.Tables)
                {
                    AddNewTable(desiredSchema.Name, table, actions);
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
                    logger.LogDebug("Schema '{OldName}' renamed to '{NewName}'", matchingCurrent.Name,
                        desiredSchema.Name);
                    actions.Add(new RenameSchema(matchingCurrent.Name, desiredSchema.Name));
                }

                CompareTables(desiredSchema.Name, matchingCurrent.Tables, desiredSchema.Tables, actions);
            }
        }
    }

    private void CompareTables(string schemaName, IReadOnlyList<Table> current, IReadOnlyList<Table> desired, List<SchemaAction> actions)
    {
        foreach (var currentTable in current)
        {
            if (desired.Any(d => d.Name == currentTable.Name || d.PreviousName == currentTable.Name))
            {
                logger.LogDebug("Table '{Schema}.{Table}' exists in desired state", schemaName, currentTable.Name);
            }
            else
            {
                logger.LogDebug("Table '{Schema}.{Table}' not found in desired state", schemaName, currentTable.Name);
                actions.Add(new DropTable(schemaName, currentTable.Name));
            }
        }

        foreach (var desiredTable in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredTable.Name || c.Name == desiredTable.PreviousName);
            if (matchingCurrent is null)
            {
                logger.LogDebug("Table '{Schema}.{Table}' is new", schemaName, desiredTable.Name);
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
                    logger.LogDebug("Table '{Schema}.{OldName}' renamed to '{NewName}'", schemaName, matchingCurrent.Name, desiredTable.Name);
                    actions.Add(new RenameTable(schemaName, matchingCurrent.Name, desiredTable.Name));
                }

                CompareColumns(schemaName, desiredTable.Name, matchingCurrent.Columns, desiredTable.Columns, actions);
                ComparePrimaryKey(schemaName, desiredTable.Name, matchingCurrent.PrimaryKey, desiredTable.PrimaryKey, actions);
                CompareForeignKeys(schemaName, desiredTable.Name, matchingCurrent.ForeignKeys ?? [], desiredTable.ForeignKeys ?? [], actions);
                CompareIndexes(schemaName, desiredTable.Name, matchingCurrent.Indexes ?? [], desiredTable.Indexes ?? [], actions);
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
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' not found in desired state", schemaName, tableName, currentCol.Name);
                actions.Add(new DropColumn(schemaName, tableName, currentCol.Name));
            }
        }

        foreach (var desiredCol in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredCol.Name || c.Name == desiredCol.PreviousName);
            if (matchingCurrent is null)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' is new", schemaName, tableName, desiredCol.Name);
                actions.Add(new AddColumn(schemaName, tableName, desiredCol));
                continue;
            }

            if (matchingCurrent.Name == desiredCol.Name)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' is unchanged", schemaName, tableName, desiredCol.Name);
            }
            else
            {
                logger.LogDebug("Column '{Schema}.{Table}.{OldName}' renamed to '{NewName}'", schemaName, tableName, matchingCurrent.Name, desiredCol.Name);
                actions.Add(new RenameColumn(schemaName, tableName, matchingCurrent.Name, desiredCol.Name));
            }

            if (matchingCurrent.Type == desiredCol.Type)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' type is unchanged ({Type})", schemaName, tableName, desiredCol.Name, desiredCol.Type);
            }
            else
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' type changed: {OldType} -> {NewType}", schemaName, tableName, desiredCol.Name, matchingCurrent.Type, desiredCol.Type);
                actions.Add(new AlterColumnType(schemaName, tableName, desiredCol.Name, matchingCurrent.Type, desiredCol.Type));
            }

            if (matchingCurrent.IsNullable == desiredCol.IsNullable)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' nullability is unchanged ({Nullability})",
                    schemaName, tableName, desiredCol.Name, desiredCol.IsNullable ? "NULL" : "NOT NULL");
            }
            else
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' nullability changed: {OldValue} -> {NewValue}", schemaName, tableName, desiredCol.Name, matchingCurrent.IsNullable, desiredCol.IsNullable);
                actions.Add(new AlterColumnNullability(schemaName, tableName, desiredCol.Name, matchingCurrent.IsNullable, desiredCol.IsNullable));
            }

            if (matchingCurrent.DefaultExpression == desiredCol.DefaultExpression)
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' default is unchanged ({Default})", schemaName, tableName, desiredCol.Name, desiredCol.DefaultExpression ?? "no default");
            }
            else
            {
                logger.LogDebug("Column '{Schema}.{Table}.{Column}' default changed: '{OldDefault}' -> '{NewDefault}'",
                    schemaName, tableName, desiredCol.Name, matchingCurrent.DefaultExpression,
                    desiredCol.DefaultExpression);
                actions.Add(new SetColumnDefault(schemaName, tableName, desiredCol.Name,
                    matchingCurrent.DefaultExpression, desiredCol.DefaultExpression));
            }
        }
    }

    private void ComparePrimaryKey(string schemaName, string tableName, PrimaryKey? current, PrimaryKey? desired,
        List<SchemaAction> actions)
    {
        if (current?.Equals(desired) ?? desired == null)
        {
            logger.LogDebug("Primary key for '{Schema}.{Table}' is unchanged", schemaName, tableName);
            return;
        }

        if (current is not null)
        {
            logger.LogDebug("Dropping primary key '{KeyName}' from '{Schema}.{Table}'", current.Name, schemaName,
                tableName);
            actions.Add(new DropPrimaryKey(schemaName, tableName, current.Name));
        }

        if (desired is not null)
        {
            logger.LogDebug("Adding primary key '{KeyName}' to '{Schema}.{Table}'", desired.Name, schemaName,
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
                logger.LogDebug("Foreign key '{FkName}' on '{Schema}.{Table}' is missing or changed", currentFk.Name,
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
                logger.LogDebug("Foreign key '{FkName}' on '{Schema}.{Table}' is new or changed", desiredFk.Name,
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
                logger.LogDebug("Index '{IndexName}' on '{Schema}.{Table}' is missing or changed", currentIdx.Name,
                    schemaName, tableName);
                actions.Add(new DropIndex(schemaName, tableName, currentIdx.Name));
            }
            else
            {
                logger.LogDebug("Index '{IndexName}' on '{Schema}.{Table}' is unchanged", currentIdx.Name, schemaName,
                    tableName);
            }
        }

        foreach (var desiredIdx in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredIdx.Name);
            if (matchingCurrent is null || !matchingCurrent.Equals(desiredIdx))
            {
                logger.LogDebug("Index '{IndexName}' on '{Schema}.{Table}' is new or changed", desiredIdx.Name,
                    schemaName, tableName);
                actions.Add(new CreateIndex(schemaName, tableName, desiredIdx));
            }
            else
            {
                logger.LogDebug("Index '{IndexName}' on '{Schema}.{Table}' is unchanged", desiredIdx.Name, schemaName,
                    tableName);
            }
        }
    }

    private void AddNewTable(string schemaName, Table table, List<SchemaAction> actions)
    {
        logger.LogDebug("Creating table '{Schema}.{Table}'", schemaName, table.Name);
        actions.Add(new CreateTable(schemaName, table));

        foreach (var fk in table.ForeignKeys ?? [])
        {
            logger.LogDebug("Adding foreign key '{FkName}' to new table '{Schema}.{Table}'", fk.Name, schemaName, table.Name);
            actions.Add(new AddForeignKey(schemaName, table.Name, fk));
        }

        foreach (var idx in table.Indexes ?? [])
        {
            logger.LogDebug("Adding index '{IndexName}' to new table '{Schema}.{Table}'", idx.Name, schemaName, table.Name);
            actions.Add(new CreateIndex(schemaName, table.Name, idx));
        }
    }
}
