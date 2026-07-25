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
    /// <returns>The dependency-ordered action list.</returns>
    IReadOnlyList<MigrationAction> Linearize(DatabaseDiff diff);
}
