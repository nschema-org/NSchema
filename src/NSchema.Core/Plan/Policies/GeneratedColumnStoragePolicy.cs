using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Plugins;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports a generated column the target engine cannot leave unstored.
/// </summary>
internal sealed class GeneratedColumnStoragePolicy(SqlDialect? dialect = null) : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        if (dialect is null || dialect.SupportsVirtualGeneratedColumns)
        {
            return [];
        }

        var sites = plan.Diff.Schemas
            .SelectMany(schema => schema.Tables.Select(table => (Schema: schema.Name, Table: table)))
            .SelectMany(x => Virtual(x.Schema, x.Table))
            .ToList();

        return sites.Count == 0 ? [] : [GeneratedColumnStorageDiagnostics.VirtualNotSupported(string.Join(", ", sites))];
    }

    // Only what this plan creates is worth reporting: a column it is not touching already exists with whatever
    // storage it was created with, and nothing here would change it.
    private static IEnumerable<string> Virtual(SqlIdentifier schema, TableDiff table)
    {
        var owner = new ObjectAddress(schema, table.Name);

        foreach (var column in table.Columns)
        {
            if (column is { Change: ChangeKind.Add, Definition: { GeneratedExpression: not null, IsStored: false } })
            {
                yield return $"column '{column.Name}' on '{owner}'";
            }
        }
    }
}
