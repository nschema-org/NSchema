using NSchema.Model;

namespace NSchema.Operations;

/// <summary>
/// Arguments for an operation run.
/// </summary>
public sealed record DriftArguments
{
    /// <summary>
    /// The schemas to scope the drift check to. When <see langword="null"/>, the whole recorded state is checked.
    /// </summary>
    public PlanningScope Scope { get; init; } = PlanningScope.All;
}
