using NSchema.Diff.Domain;

namespace NSchema.Operations;

/// <summary>
/// The result of a drift check: the recorded → live diff, and whether the live database has drifted.
/// </summary>
/// <param name="Diff">The diff describing how the live database has drifted from the recorded state.</param>
public sealed record DriftResult(DatabaseDiff Diff)
{
    /// <summary>
    /// Whether the live database has drifted from the recorded state (a non-empty diff).
    /// </summary>
    public bool HasDrift => !Diff.IsEmpty;
}
