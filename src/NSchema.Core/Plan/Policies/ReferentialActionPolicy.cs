using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Tables;
using NSchema.Model;
using NSchema.Model.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Plugins;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports a referential action the target engine cannot honour.
/// </summary>
internal sealed class ReferentialActionPolicy(SqlDialect? dialect = null) : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        if (dialect is null || dialect.SupportsRestrict)
        {
            return [];
        }

        var sites = plan.Diff.Schemas
            .SelectMany(schema => schema.Tables.Select(table => (Schema: schema.Name, Table: table)))
            .SelectMany(x => Restricted(x.Schema, x.Table))
            .ToList();

        return sites.Count == 0 ? [] : [ReferentialActionDiagnostics.RestrictNotSupported(string.Join(", ", sites))];
    }

    // Only what this plan creates is worth reporting: a foreign key it is not touching is already in the database
    // with whatever actions it was created with, and nothing here would change them.
    private static IEnumerable<string> Restricted(SqlIdentifier schema, TableDiff table)
    {
        var owner = new ObjectAddress(schema, table.Name);

        foreach (var key in table.ForeignKeys)
        {
            if (key is not { Change: ChangeKind.Add, Definition: { } definition })
            {
                continue;
            }

            var clauses = new List<string>();

            if (definition.OnDelete == ReferentialAction.Restrict)
            {
                clauses.Add("ON DELETE RESTRICT");
            }

            if (definition.OnUpdate == ReferentialAction.Restrict)
            {
                clauses.Add("ON UPDATE RESTRICT");
            }

            if (clauses.Count > 0)
            {
                yield return $"{string.Join(" and ", clauses)} on foreign key '{key.Name}' on '{owner}'";
            }
        }
    }
}
