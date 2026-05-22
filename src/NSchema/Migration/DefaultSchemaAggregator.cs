using NSchema.Schema;

namespace NSchema.Migration;

internal sealed class DefaultSchemaAggregator : ISchemaAggregator
{
    public DatabaseSchema Aggregate(IEnumerable<DatabaseSchema> schemas)
    {
        var all = schemas.ToList();

        var mergedSchemas = all
            .SelectMany(db => db.Schemas)
            .GroupBy(s => s.Name)
            .Select(s => AggregateSchemaGroup(s.ToList()))
            .ToList();

        var droppedSchemas = all
            .SelectMany(db => db.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DatabaseSchema(mergedSchemas, droppedSchemas);
    }

    private static SchemaDefinition AggregateSchemaGroup(IReadOnlyList<SchemaDefinition> schemas)
    {
        var tables = new List<Table>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string schemaName = schemas[0].Name;
        foreach (var schema in schemas)
        {
            foreach (var table in schema.Tables)
            {
                if (!seen.Add(table.Name))
                {
                    throw new InvalidOperationException($"Duplicate table '{table.Name}' found in schema '{schemaName}' across multiple providers.");
                }

                tables.Add(table);
            }
        }

        var comments = schemas.Select(s => s.Comment).Where(c => c is not null).Distinct().ToList();
        if (comments.Count > 1)
        {
            throw new InvalidOperationException($"Conflicting comments specified for schema '{schemaName}' across multiple providers.");
        }
        string? comment = comments.FirstOrDefault();

        bool isPartial = schemas.Any(s => s.IsPartial);
        var droppedTables = schemas
            .SelectMany(s => s.DroppedTables)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string? oldName = schemas.Select(s => s.OldName).FirstOrDefault(n => n is not null);

        var grants = schemas
            .SelectMany(s => s.Grants)
            .Distinct()
            .ToList();

        return new SchemaDefinition(schemaName, oldName, isPartial, comment, tables, droppedTables, grants);
    }
}
