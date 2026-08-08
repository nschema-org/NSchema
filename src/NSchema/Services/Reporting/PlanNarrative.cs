using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;

namespace NSchema.Services.Reporting;

/// <summary>
/// The wording the text faces share when narrating a plan.
/// </summary>
internal static class PlanNarrative
{
    /// <summary>
    /// The adopted identities as dotted names, in a stable order.
    /// </summary>
    public static IReadOnlyList<string> AdoptedNames(IdentitySet adopted) =>
    [
        .. adopted.DatabaseObjects.Select(o => o.Value)
            .Concat(adopted.SchemaObjects.Select(o => o.Value))
            .Order(),
    ];

    /// <summary>
    /// The heading above the objects the apply takes over.
    /// </summary>
    public static string AdoptionHeading(int count) =>
        $"Adopting {(count == 1 ? "1 existing object" : $"{count} existing objects")} into management:";

    /// <summary>
    /// The plan's prospective counts. Adoption is listed only when the plan takes something over.
    /// </summary>
    public static string Counts(DiffSummary summary, int adopted) =>
        $"{summary.Added} to add, {summary.Modified} to change, {summary.Removed} to destroy"
        + (adopted > 0 ? $", {adopted} to adopt" : string.Empty);

    /// <summary>
    /// The one-line plan preview the quiet face renders: prospective counts plus the statements the plan would run.
    /// </summary>
    public static string Summary(MigrationPlan plan) =>
        $"Plan: {Counts(plan.Diff.GetSummary(), AdoptedCount(plan.Adopted))} ({Statements(plan.Statements.Count)}).";

    /// <summary>
    /// The one-line diff preview the quiet face renders when there is no plan — a drift check's comparison.
    /// </summary>
    public static string Summary(DatabaseDiff diff) => $"Plan: {Counts(diff.GetSummary(), adopted: 0)}.";

    /// <summary>
    /// Retrospectively describes the changes in <paramref name="diff"/>, for the recap a finished run reports.
    /// </summary>
    public static string Describe(DatabaseDiff diff)
    {
        var (added, modified, removed) = diff.GetSummary();

        var changes = new List<string>(3);
        if (added > 0)
        {
            changes.Add($"{added} added");
        }

        if (modified > 0)
        {
            changes.Add($"{modified} changed");
        }

        if (removed > 0)
        {
            changes.Add($"{removed} destroyed");
        }

        return changes.Count > 0 ? string.Join(", ", changes) : "no changes";
    }

    /// <summary>
    /// Retrospectively describes the changes in <paramref name="plan"/> together with the number of SQL statements that ran.
    /// </summary>
    public static string Describe(MigrationPlan plan)
    {
        var adopted = AdoptedCount(plan.Adopted);
        var takeover = adopted > 0 ? $", {adopted} adopted" : string.Empty;

        return $"{Describe(plan.Diff)}{takeover} ({Statements(plan.Statements.Count)})";
    }

    private static string Statements(int count) => count == 1 ? "1 statement" : $"{count} statements";

    private static int AdoptedCount(IdentitySet adopted) => adopted.DatabaseObjects.Count + adopted.SchemaObjects.Count;
}
