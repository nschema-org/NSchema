using NSchema.Domain.Execution;
using NSchema.Domain.Schema;

namespace NSchema.Diffing;

public sealed class DatabaseModelDiffer : ISchemaDiffer
{
    public IReadOnlyList<SchemaInstruction> Diff(DatabaseModel current, DatabaseModel desired)
    {
        var instructions = new InstructionSet();

        foreach (var script in desired.PreDeploymentScripts ?? [])
            instructions.PreScripts.Add(new RunPreDeploymentScript(script));

        DiffSchemas(current.Schemas, desired.Schemas, instructions);

        foreach (var script in desired.PostDeploymentScripts ?? [])
            instructions.PostScripts.Add(new RunPostDeploymentScript(script));

        return instructions.ToList();
    }

    private static void DiffSchemas(
        IReadOnlyList<DatabaseSchema> current,
        IReadOnlyList<DatabaseSchema> desired,
        InstructionSet instructions)
    {
        foreach (var currentSchema in current)
        {
            if (!desired.Any(d => d.Name == currentSchema.Name || d.PreviousName == currentSchema.Name))
                instructions.SchemaDrops.Add(new DropSchema(currentSchema.Name));
        }

        foreach (var desiredSchema in desired)
        {
            var matchingCurrent = FindMatch(current, desiredSchema.Name, desiredSchema.PreviousName);

            if (matchingCurrent is null)
            {
                instructions.SchemaCreates.Add(new CreateSchema(desiredSchema.Name));
                foreach (var table in desiredSchema.Tables)
                    AddNewTable(desiredSchema.Name, table, instructions);
            }
            else
            {
                if (matchingCurrent.Name != desiredSchema.Name)
                    instructions.SchemaRenames.Add(new RenameSchema(matchingCurrent.Name, desiredSchema.Name));

                DiffTables(desiredSchema.Name, matchingCurrent.Tables, desiredSchema.Tables, instructions);
            }
        }
    }

    private static void DiffTables(
        string schemaName,
        IReadOnlyList<Table> current,
        IReadOnlyList<Table> desired,
        InstructionSet instructions)
    {
        foreach (var currentTable in current)
        {
            if (!desired.Any(d => d.Name == currentTable.Name || d.PreviousName == currentTable.Name))
                instructions.TableDrops.Add(new DropTable(schemaName, currentTable.Name));
        }

        foreach (var desiredTable in desired)
        {
            var matchingCurrent = FindMatch(current, desiredTable.Name, desiredTable.PreviousName);

            if (matchingCurrent is null)
            {
                AddNewTable(schemaName, desiredTable, instructions);
            }
            else
            {
                if (matchingCurrent.Name != desiredTable.Name)
                    instructions.TableRenames.Add(new RenameTable(schemaName, matchingCurrent.Name, desiredTable.Name));

                DiffColumns(schemaName, desiredTable.Name, matchingCurrent.Columns, desiredTable.Columns, instructions);
                DiffPrimaryKey(schemaName, desiredTable.Name, matchingCurrent.PrimaryKey, desiredTable.PrimaryKey,
                    instructions);
                DiffForeignKeys(schemaName, desiredTable.Name, matchingCurrent.ForeignKeys ?? [],
                    desiredTable.ForeignKeys ?? [], instructions);
                DiffIndexes(schemaName, desiredTable.Name, matchingCurrent.Indexes ?? [], desiredTable.Indexes ?? [],
                    instructions);
            }
        }
    }

    private static void DiffColumns(
        string schemaName,
        string tableName,
        IReadOnlyList<Column> current,
        IReadOnlyList<Column> desired,
        InstructionSet instructions)
    {
        foreach (var currentCol in current)
        {
            if (!desired.Any(d => d.Name == currentCol.Name || d.PreviousName == currentCol.Name))
                instructions.ColumnDrops.Add(new DropColumn(schemaName, tableName, currentCol.Name));
        }

        foreach (var desiredCol in desired)
        {
            var matchingCurrent = FindMatch(current, desiredCol.Name, desiredCol.PreviousName);

            if (matchingCurrent is null)
            {
                instructions.ColumnAdds.Add(new AddColumn(schemaName, tableName, desiredCol));
                continue;
            }

            if (matchingCurrent.Name != desiredCol.Name)
                instructions.ColumnRenames.Add(new RenameColumn(schemaName, tableName, matchingCurrent.Name,
                    desiredCol.Name));

            if (matchingCurrent.Type != desiredCol.Type)
                instructions.ColumnAlters.Add(new AlterColumnType(schemaName, tableName, desiredCol.Name,
                    matchingCurrent.Type, desiredCol.Type));

            if (matchingCurrent.IsNullable != desiredCol.IsNullable)
                instructions.ColumnAlters.Add(new AlterColumnNullability(schemaName, tableName, desiredCol.Name,
                    matchingCurrent.IsNullable, desiredCol.IsNullable));

            if (matchingCurrent.DefaultExpression != desiredCol.DefaultExpression)
                instructions.ColumnAlters.Add(new SetColumnDefault(schemaName, tableName, desiredCol.Name,
                    matchingCurrent.DefaultExpression, desiredCol.DefaultExpression));
        }
    }

    private static void DiffPrimaryKey(
        string schemaName,
        string tableName,
        PrimaryKey? current,
        PrimaryKey? desired,
        InstructionSet instructions)
    {
        if (PrimaryKeysEqual(current, desired)) return;

        if (current is not null)
            instructions.PrimaryKeyDrops.Add(new DropPrimaryKey(schemaName, tableName, current.Name));

        if (desired is not null)
            instructions.PrimaryKeyAdds.Add(new AddPrimaryKey(schemaName, tableName, desired));
    }

    private static void DiffForeignKeys(
        string schemaName,
        string tableName,
        IReadOnlyList<ForeignKey> current,
        IReadOnlyList<ForeignKey> desired,
        InstructionSet instructions)
    {
        foreach (var currentFk in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentFk.Name);
            if (matchingDesired is null || !ForeignKeysEqual(currentFk, matchingDesired))
                instructions.ForeignKeyDrops.Add(new DropForeignKey(schemaName, tableName, currentFk.Name));
        }

        foreach (var desiredFk in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredFk.Name);
            if (matchingCurrent is null || !ForeignKeysEqual(matchingCurrent, desiredFk))
                instructions.ForeignKeyAdds.Add(new AddForeignKey(schemaName, tableName, desiredFk));
        }
    }

    private static void DiffIndexes(
        string schemaName,
        string tableName,
        IReadOnlyList<TableIndex> current,
        IReadOnlyList<TableIndex> desired,
        InstructionSet instructions)
    {
        foreach (var currentIdx in current)
        {
            var matchingDesired = desired.FirstOrDefault(d => d.Name == currentIdx.Name);
            if (matchingDesired is null || !IndexesEqual(currentIdx, matchingDesired))
                instructions.IndexDrops.Add(new DropIndex(schemaName, tableName, currentIdx.Name));
        }

        foreach (var desiredIdx in desired)
        {
            var matchingCurrent = current.FirstOrDefault(c => c.Name == desiredIdx.Name);
            if (matchingCurrent is null || !IndexesEqual(matchingCurrent, desiredIdx))
                instructions.IndexAdds.Add(new CreateIndex(schemaName, tableName, desiredIdx));
        }
    }

    private static void AddNewTable(string schemaName, Table table, InstructionSet instructions)
    {
        instructions.TableCreates.Add(new CreateTable(schemaName, table));

        foreach (var fk in table.ForeignKeys ?? [])
            instructions.ForeignKeyAdds.Add(new AddForeignKey(schemaName, table.Name, fk));

        foreach (var idx in table.Indexes ?? [])
            instructions.IndexAdds.Add(new CreateIndex(schemaName, table.Name, idx));
    }

    // ── Matching ─────────────────────────────────────────────────────────────

    private static T? FindMatch<T>(IReadOnlyList<T> collection, string name, string? previousName = null)
        where T : class
    {
        Func<T, string> getName = typeof(T) switch
        {
            var t when t == typeof(DatabaseSchema) => x => ((DatabaseSchema)(object)x).Name,
            var t when t == typeof(Table) => x => ((Table)(object)x).Name,
            var t when t == typeof(Column) => x => ((Column)(object)x).Name,
            _ => throw new InvalidOperationException($"Unsupported type {typeof(T).Name}")
        };

        return collection.FirstOrDefault(x => getName(x) == name)
               ?? (previousName is not null ? collection.FirstOrDefault(x => getName(x) == previousName) : null);
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    private static bool PrimaryKeysEqual(PrimaryKey? a, PrimaryKey? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Name == b.Name && a.ColumnNames.SequenceEqual(b.ColumnNames);
    }

    private static bool ForeignKeysEqual(ForeignKey a, ForeignKey b) =>
        a.Name == b.Name
        && a.ColumnNames.SequenceEqual(b.ColumnNames)
        && a.ReferencedSchema == b.ReferencedSchema
        && a.ReferencedTable == b.ReferencedTable
        && a.ReferencedColumnNames.SequenceEqual(b.ReferencedColumnNames)
        && a.OnDelete == b.OnDelete
        && a.OnUpdate == b.OnUpdate;

    private static bool IndexesEqual(TableIndex a, TableIndex b) =>
        a.Name == b.Name
        && a.IsUnique == b.IsUnique
        && a.ColumnNames.SequenceEqual(b.ColumnNames);

}
