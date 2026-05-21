using NSchema.Domain.Schema;

namespace NSchema.Desired;

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

        var preScripts = all.SelectMany(db => db.PreDeploymentScripts).ToList();
        var postScripts = all.SelectMany(db => db.PostDeploymentScripts).ToList();

        return new DatabaseSchema(mergedSchemas, preScripts, postScripts);
    }

    private static Schema MergeSchemaGroup(IGrouping<string, Schema> group)
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

        // PreviousName: use whichever provider sets it (expect at most one).
        string? previousName = group.Select(s => s.PreviousName).FirstOrDefault(n => n is not null);

        return new Schema(group.Key, tables, previousName);
    }
}
