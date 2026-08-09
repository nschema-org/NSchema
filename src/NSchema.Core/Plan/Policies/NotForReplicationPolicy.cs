using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Plugins;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports <c>NOT FOR REPLICATION</c> the target engine cannot honour. Losing it changes nothing
/// about ordinary writes and everything about what happens when a replication agent writes — which is exactly the
/// case nobody is watching, and so worth saying out loud.
/// </summary>
internal sealed class NotForReplicationPolicy(SqlDialect? dialect = null) : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        if (dialect is null || dialect.SupportsNotForReplication)
        {
            return [];
        }

        var sites = plan.Diff.Schemas
            .SelectMany(schema => schema.Tables.Select(table => (Schema: schema.Name, Table: table)))
            .SelectMany(x => Declared(x.Schema, x.Table))
            .ToList();

        return sites.Count == 0 ? [] : [NotForReplicationDiagnostics.NotForReplicationNotSupported(string.Join(", ", sites))];
    }

    private static IEnumerable<string> Declared(SqlIdentifier schema, TableDiff table)
    {
        var owner = new ObjectAddress(schema, table.Name);

        foreach (var column in table.Columns)
        {
            if (column is { Change: ChangeKind.Add, Definition.IdentityOptions.NotForReplication: true })
            {
                yield return $"the identity on column '{column.Name}' of '{owner}'";
            }
        }

        foreach (var trigger in table.Triggers)
        {
            if (trigger is { Change: ChangeKind.Add, Definition.IsNotForReplication: true })
            {
                yield return $"trigger '{trigger.Name}' on '{owner}'";
            }
        }
    }
}
