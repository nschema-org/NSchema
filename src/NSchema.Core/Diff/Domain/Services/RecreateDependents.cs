using NSchema.Model;
using NSchema.Model.Services;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Checks that nothing is still declared against a type the plan intends to recreate.
/// </summary>
internal static class RecreateDependents
{
    public static IEnumerable<Diagnostic> Check(DatabaseDiff diff, Database current)
    {
        var recreated = diff.Schemas
            .SelectMany(schema => schema.Domains
                .Where(domain => domain.RequiresRecreate)
                // Addressed as the graph keys it: the kind rides the node, not the address.
                .Select(domain => new ObjectAddress(schema.Name, domain.Name)))
            .ToList();

        if (recreated.Count == 0)
        {
            return [];
        }

        var graph = new DependencyGraph(current);
        var diagnostics = new List<Diagnostic>();

        foreach (var address in recreated)
        {
            // Columns only: a definition that names the type is rebuilt with it, but a column stands for its
            // table's rows and is what the engine refuses to drop out from under.
            var blocked = graph.AllDependentsOf(graph.At(address))
                .Where(node => node.Kind == DependencyKind.Column)
                .Select(node => node.Address)
                .Distinct()
                .ToList();

            if (blocked.Count > 0)
            {
                diagnostics.Add(DiffDiagnostics.RecreateBlockedByDependents(blocked, [address.ToString()]));
            }
        }

        return diagnostics;
    }
}
