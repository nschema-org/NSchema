using NSchema.Plan.Domain;

namespace NSchema.Operations;

/// <summary>
/// Arguments for applying a computed plan.
/// </summary>
public sealed record ApplyArguments
{
    /// <summary>
    /// The plan to execute, from a plan operation or a saved plan file.
    /// </summary>
    public required MigrationPlan Plan { get; init; }

    /// <summary>
    /// When true, policy errors found in the plan's diff are demoted to warnings and execution proceeds.
    /// Required to apply a teardown plan, whose diff is fully destructive by design.
    /// </summary>
    public bool Force { get; init; }
}
