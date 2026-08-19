using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Plugins;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports a row-guid column the target engine cannot mark as one.
/// </summary>
internal sealed class RowGuidPolicy(SqlDialect? dialect = null) : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        if (dialect is null || dialect.SupportsRowGuidColumns)
        {
            return [];
        }

        var sites = plan.Diff.Schemas
            .SelectMany(schema => schema.Tables.Select(table => (Schema: schema.Name, Table: table)))
            .SelectMany(x => RowGuids(x.Schema, x.Table))
            .ToList();

        return sites.Count == 0 ? [] : [RowGuidDiagnostics.RowGuidNotSupported(string.Join(", ", sites))];
    }

    private static IEnumerable<string> RowGuids(SqlIdentifier schema, TableDiff table)
    {
        var owner = new ObjectAddress(schema, table.Name);

        foreach (var column in table.Columns)
        {
            if (column is { Change: ChangeKind.Add, Definition.IsRowGuid: true })
            {
                yield return $"column '{column.Name}' on '{owner}'";
            }
        }
    }
}
