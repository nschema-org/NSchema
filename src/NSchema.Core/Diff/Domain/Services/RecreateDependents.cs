using NSchema.Model;
using NSchema.Model.Services;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Checks that nothing is still declared against objects the plan intends to recreate.
/// </summary>
internal static class RecreateDependents
{
    public static IEnumerable<Diagnostic> Check(DatabaseDiff diff, Database current)
    {
        var graph = new DependencyGraph(current);
        var diagnostics = new List<Diagnostic>();

        CheckRecreatedDomains(diff, graph, diagnostics);
        CheckRecreatedComposites(diff, graph, diagnostics);

        return diagnostics;
    }

    private static void CheckRecreatedComposites(DatabaseDiff diff, DependencyGraph graph, List<Diagnostic> diagnostics)
    {
        // Adding or dropping a field is allowed while the type is in use; changing one's type is not, and no
        // spelling of the statement gets round it, so it is reported rather than rendered and left to fail.
        var retyped = diff.Schemas
            .SelectMany(schema => schema.CompositeTypes
                .Where(composite => composite.Fields.Any(field => field.Type is not null))
                .Select(composite => new ObjectAddress(schema.Name, composite.Name)))
            .ToList();

        foreach (var address in retyped)
        {
            if (BlockedBy(graph, address) is { Count: > 0 } blocked)
            {
                diagnostics.Add(DiffDiagnostics.RetypeBlockedByDependents(blocked, [address.ToString()]));
            }
        }
    }

    private static void CheckRecreatedDomains(DatabaseDiff diff, DependencyGraph graph, List<Diagnostic> diagnostics)
    {
        var recreated = diff.Schemas
            .SelectMany(schema => schema.Domains
                .Where(domain => domain.RequiresRecreate)
                // Addressed as the graph keys it: the kind rides the node, not the address.
                .Select(domain => new ObjectAddress(schema.Name, domain.Name)))
            .ToList();

        foreach (var address in recreated)
        {
            if (BlockedBy(graph, address) is { Count: > 0 } blocked)
            {
                diagnostics.Add(DiffDiagnostics.RecreateBlockedByDependents(blocked, [address.ToString()]));
            }
        }
    }

    // Columns only: a definition that names the type is rebuilt with it, but a column stands for its table's rows
    // and is what the engine refuses to change out from under.
    private static List<Address> BlockedBy(DependencyGraph graph, ObjectAddress address) =>
        [.. graph.AllDependentsOf(graph.At(address))
            .Where(node => node.Kind == DependencyKind.Column)
            .Select(node => node.Address)
            .Distinct()];
}
