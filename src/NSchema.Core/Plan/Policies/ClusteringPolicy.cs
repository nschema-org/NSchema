using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Plugins;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports clustering the target engine cannot honour. Clustering is a physical-ordering
/// facet only some engines have, so a project written for one and applied to another would otherwise create
/// the objects silently unclustered — the schema looks right and the row order is not what was asked for.
/// </summary>
/// <param name="dialect">
/// The dialect the plan renders through, which is what knows whether the engine has clustering at all;
/// <see langword="null"/> when no provider is loaded, in which case there is nothing to judge against.
/// </param>
internal sealed class ClusteringPolicy(SqlDialect? dialect = null) : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        if (dialect is null || dialect.SupportsClustering)
        {
            return [];
        }

        var sites = plan.Diff.Schemas
            .SelectMany(schema => schema.Tables.Select(table => (Schema: schema.Name, Table: table)))
            .SelectMany(x => Clustered(x.Schema, x.Table))
            .ToList();

        return sites.Count == 0 ? [] : [ClusteringDiagnostics.ClusteringNotSupported(string.Join(", ", sites))];
    }

    // Only what this plan creates is worth reporting: a member it is not touching is already in the database,
    // however it was ordered, and nothing here would change that.
    private static IEnumerable<string> Clustered(SqlIdentifier schema, TableDiff table)
    {
        var owner = new ObjectAddress(schema, table.Name);

        foreach (var index in table.Indexes.Where(i => i.IsAdd() && i.Definition.Clustered is true))
        {
            yield return $"index '{index.Name}' on '{owner}'";
        }

        foreach (var key in table.PrimaryKeys.Where(k => k is { Change: ChangeKind.Add, Definition.Clustered: true }))
        {
            yield return $"primary key '{key.Name}' on '{owner}'";
        }

        foreach (var unique in table.UniqueConstraints.Where(u => u is { Change: ChangeKind.Add, Definition.Clustered: true }))
        {
            yield return $"unique constraint '{unique.Name}' on '{owner}'";
        }
    }
}
