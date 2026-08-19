using NSchema.Diff.Domain;

namespace NSchema.Plan.Domain.Services;

/// <summary>
/// Linearizes a structured <see cref="DatabaseDiff"/> into an executable list of actions.
/// </summary>
internal interface IPlanLinearizer
{
    /// <summary>
    /// Produces the ordered migration plan that realizes the given diff.
    /// </summary>
    /// <param name="diff">The structured diff to linearize.</param>
    /// <param name="dependencies">The edges ordering the objects the diff touches against each other.</param>
    /// <param name="capabilities">What the target database can do, where that decides the shape of an action.</param>
    /// <returns>The dependency-ordered action list.</returns>
    IReadOnlyList<MigrationAction> Linearize(DatabaseDiff diff, PlanDependencies dependencies, DialectCapabilities capabilities);
}
