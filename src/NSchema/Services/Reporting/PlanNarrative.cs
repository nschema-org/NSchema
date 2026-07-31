using NSchema.Diff.Domain;
using NSchema.Model;

namespace NSchema.Services.Reporting;

/// <summary>
/// The wording the text faces share when presenting a plan, so the Spectre and Markdown renderings agree.
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
    /// The plan's footer counts. Adoption is listed only when the plan takes something over.
    /// </summary>
    public static string Counts(DiffSummary summary, int adopted) =>
        $"{summary.Added} to add, {summary.Modified} to change, {summary.Removed} to destroy"
        + (adopted > 0 ? $", {adopted} to adopt." : ".");
}
