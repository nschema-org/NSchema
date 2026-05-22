using NSchema.Schema;

namespace NSchema.Migration;

public sealed class DefaultSchemaAggregator : ISchemaAggregator
{
    public DatabaseSchema Aggregate(IEnumerable<DatabaseSchema> schemas)
    {
        var all = schemas.ToList();

        var mergedSchemas = all
            .SelectMany(db => db.Schemas)
            .GroupBy(s => s.Name)
            .Select(MergeSchemaGroup)
            .ToList();

        var droppedSchemas = all
            .SelectMany(db => db.DroppedSchemas)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DatabaseSchema(mergedSchemas, droppedSchemas.Count > 0 ? droppedSchemas : null);
    }

    private static SchemaDefinition MergeSchemaGroup(IGrouping<string, SchemaDefinition> group)
    {
        var tables = new List<Table>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var schema in group)
        {
            foreach (var table in schema.Tables)
            {
                if (!seen.Add(table.Name))
                {
                    throw new InvalidOperationException(
                        $"Duplicate table '{table.Name}' found in schema '{group.Key}' across multiple providers.");
                }

                tables.Add(table);
            }
        }

        bool isPartial = group.Any(s => s.IsPartial);
        var droppedTables = group
            .SelectMany(s => s.DroppedTables)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        string? previousName = group.Select(s => s.PreviousName).FirstOrDefault(n => n is not null);

        var comments = group.Select(s => s.Comment).Where(c => c is not null).Distinct().ToList();
        if (comments.Count > 1)
        {
            throw new InvalidOperationException(
                $"Conflicting comments specified for schema '{group.Key}' across multiple providers.");
        }
        string? comment = comments.FirstOrDefault();

        var grants = group
            .SelectMany(s => s.Grants)
            .Distinct()
            .ToList();

        return new SchemaDefinition(group.Key, previousName, isPartial, comment, tables, droppedTables, grants);
    }
}
