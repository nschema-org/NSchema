using NSchema.Diff.Domain.Schemas;
using NSchema.Diff.Domain.Tables;
using NSchema.Plan.Domain;
using NSchema.Plan.Plugins;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports documentation the target engine cannot record.
/// </summary>
internal sealed class CommentStoragePolicy(SqlDialect? dialect = null) : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        if (dialect is null || dialect.SupportsComments)
        {
            return [];
        }

        var sites = plan.Diff.Schemas.SelectMany(Documented).ToList();

        return sites.Count == 0 ? [] : [CommentStorageDiagnostics.NotSupported(string.Join(", ", sites))];
    }

    // Walked through the diff's own enumerators rather than its collections one by one: every object and every
    // member carries a comment, and listing the kinds here would be a list to forget a kind from.
    private static IEnumerable<string> Documented(SchemaDiff schema)
    {
        if (schema.Comment is not null)
        {
            yield return $"schema '{schema.Name}'";
        }

        foreach (var declared in schema.EnumerateObjects())
        {
            if (declared.Comment is not null)
            {
                yield return $"'{schema.Name}.{declared.Name}'";
            }

            if (declared is not TableDiff table)
            {
                continue;
            }

            foreach (var member in table.EnumerateMembers().Where(member => member.Comment is not null))
            {
                yield return $"'{schema.Name}.{table.Name}.{member.Name}'";
            }
        }
    }
}
