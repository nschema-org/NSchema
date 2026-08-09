using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Plugins;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports a default constraint name the target engine cannot give. The default still applies;
/// what is lost is being able to name it later, which is only worth knowing because the alternative is an engine's
/// own unpredictable name.
/// </summary>
internal sealed class NamedDefaultPolicy(SqlDialect? dialect = null) : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        if (dialect is null || dialect.SupportsNamedDefaults)
        {
            return [];
        }

        var sites = plan.Diff.Schemas
            .SelectMany(schema => schema.Tables.Select(table => (Schema: schema.Name, Table: table)))
            .SelectMany(x => Named(x.Schema, x.Table))
            .ToList();

        return sites.Count == 0 ? [] : [NamedDefaultDiagnostics.NamedDefaultNotSupported(string.Join(", ", sites))];
    }

    private static IEnumerable<string> Named(SqlIdentifier schema, TableDiff table)
    {
        var owner = new ObjectAddress(schema, table.Name);

        foreach (var column in table.Columns)
        {
            if (column is { Change: ChangeKind.Add, Definition.DefaultConstraintName: not null })
            {
                yield return $"column '{column.Name}' on '{owner}'";
            }
        }
    }
}
